using Nhaama.Memory;
using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;

namespace XIVLauncher
{
    class XIVGame
    {
        // The user agent for frontier pages. {0} has to be replaced by a unique computer id and it's checksum
        private static readonly string UserAgentTemplate = "SQEXAuthor/2.0.0(Windows 6.2; ja-jp; {0})";
        private string _userAgent = GetUserAgent();

        private static readonly string[] FilesToHash =
        {
            "ffxivboot.exe", 
            "ffxivboot64.exe",
            "ffxivlauncher.exe",
            "ffxivlauncher64.exe",
            "ffxivupdater.exe",
            "ffxivupdater64.exe",
        };

        public void Login(string username, string password, string otp)
        {
            var loginResult = OauthLogin(username, password, otp);

            if (!loginResult.Playable)
            {
                MessageBox.Show("This Square Enix account cannot play FINAL FANTASY XIV.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!loginResult.TermsAccepted)
            {
                MessageBox.Show("Please accept the FINAL FANTASY XIV Terms of Use in the official launcher.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Clamp the expansion level to what the account is allowed to access
            var expansionLevel = Math.Min(Math.Max(loginResult.MaxExpansion, 0), Settings.GetExpansionLevel());
            var (sid, needsUpdate) = RegisterSession(loginResult);

            if (needsUpdate)
            {
                MessageBox.Show(
                    "Your game is out of date. Please start the official launcher and update it, before trying to log in.",
                    "Out of date", MessageBoxButton.OK, MessageBoxImage.Error);

                return;
            }

            LaunchGame(sid, loginResult.Region, expansionLevel);
        }

        private static void LaunchGame(string sessionId, int region, int expansionLevel, bool closeMutants = true)
        {
            try {
                var game = new Process();
                if (Settings.IsDX11()) { game.StartInfo.FileName = Settings.GetGamePath() + "/game/ffxiv_dx11.exe"; } else { game.StartInfo.FileName = Settings.GetGamePath() + "/game/ffxiv.exe"; }
                game.StartInfo.Arguments = $"DEV.DataPathType=1 DEV.MaxEntitledExpansionID={expansionLevel} DEV.TestSID={sessionId} DEV.UseSqPack=1 SYS.Region={region} language={(int) Settings.GetLanguage()} ver={GetLocalGamever()}";
                game.Start();

                if (closeMutants)
                {
                    for (var tries = 0; tries < 5; tries++)
                    {
                        // Is the main window open? That means the mutants must be too
                        if(game.MainWindowHandle == IntPtr.Zero)
                        {
                            Thread.Sleep(5000);
                            continue;
                        }

                        CloseMutants(game);
                        break;
                    }
                }
                
            }
            catch (Exception exc)
            {
                MessageBox.Show("Could not launch executable. Is your game path correct?\n\n" + exc, "Launch failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void CloseMutants(Process process)
        {
            var nhaamaProcess = process.GetNhaamaProcess();

            var handles = nhaamaProcess.GetHandles();

            // Check if handle is a ffxiv mutant and close it
            foreach (var nhaamaHandle in handles)
            {
                if(nhaamaHandle.Name.Contains("ffxiv_game0"))
                    nhaamaHandle.Close();
            }
        }

        private static string GetBootVersionHash()
        {
            string result = "";

            for (int i = 0; i < FilesToHash.Length; i++)
            {
                result += $"{FilesToHash[i]}/{GetFileHash(Path.Combine(Settings.GetGamePath(), "boot", FilesToHash[i]))}";

                if (i != FilesToHash.Length - 1)
                    result += ",";
            }

            return result;
        }

        private static (string Sid, bool NeedsUpdate) RegisterSession(OauthLoginResult loginResult)
        {
            using (WebClient client = new WebClient())
            {
                client.Headers.Add("X-Hash-Check", "enabled");
                client.Headers.Add("user-agent", "FFXIV PATCH CLIENT");
                client.Headers.Add("Referer", $"https://ffxiv-login.square-enix.com/oauth/ffxivarr/login/top?lng=en&rgn={loginResult.Region}");
                client.Headers.Add("Content-Type", "application/x-www-form-urlencoded");

                InitiateSslTrust();

                var url = "https://patch-gamever.ffxiv.com/http/win32/ffxivneo_release_game/" + GetLocalGamever() +
                          "/" + loginResult.SessionId;

                try
                {
                    var result = client.UploadString(url, GetBootVersionHash());

                    if (client.ResponseHeaders.AllKeys.Contains("X-Patch-Unique-Id"))
                    {
                        var sid = client.ResponseHeaders["X-Patch-Unique-Id"];

                        return (sid, result != string.Empty);
                    }
                }
                catch (WebException exc)
                {
                    if (exc.Status == WebExceptionStatus.ProtocolError)
                    {
                        var response = exc.Response as HttpWebResponse;
                        if (response != null)
                        {
                            // This apparently can also indicate that we need to update
                            if(response.StatusCode == HttpStatusCode.Conflict)
                                return ("", true);
                        }
                        else
                        {
                            throw;
                        }
                    }
                    else
                    {
                        throw;
                    }
                }
                
                throw new Exception("Could not validate game version.");
            }
        }


        private string GetStored() //this is needed to be able to access the login site correctly
        {
            WebClient loginInfo = new WebClient();
            loginInfo.Headers.Add("user-agent", _userAgent);
            string reply = loginInfo.DownloadString("https://ffxiv-login.square-enix.com/oauth/ffxivarr/login/top?lng=en&rgn=3&isft=0&issteam=0");

            Regex storedre = new Regex(@"\t<\s*input .* name=""_STORED_"" value=""(?<stored>.*)"">");

            var stored = storedre.Matches(reply)[0].Groups["stored"].Value;
            return stored;
        }

        internal class OauthLoginResult
        {
            public string SessionId { get; set; }
            public int Region { get; set; }
            public bool TermsAccepted { get; set; }
            public bool Playable { get; set; }
            public int MaxExpansion { get; set; }
        }

        private OauthLoginResult OauthLogin(string username, string password, string otp)
        {
            using (WebClient client = new WebClient())
            {
                client.Headers.Add("user-agent", _userAgent);
                client.Headers.Add("Referer", "https://ffxiv-login.square-enix.com/oauth/ffxivarr/login/top?lng=en&rgn=3&isft=0&issteam=0");
                client.Headers.Add("Content-Type", "application/x-www-form-urlencoded");

                byte[] response =
                client.UploadValues("https://ffxiv-login.square-enix.com/oauth/ffxivarr/login/login.send", new NameValueCollection() //get the session id with user credentials
                {
                    { "_STORED_", GetStored() },
                    { "sqexid", username },
                    { "password", password },
                    { "otppw", otp }
                });

                string reply = System.Text.Encoding.UTF8.GetString(response);

                var regex = new Regex(@"window.external.user\(""login=auth,ok,(?<launchParams>.*)\);");
                var matches = regex.Matches(reply);

                if(matches.Count == 0)
                    throw new Exception("Could not log in to oauth.");

                var launchParams = matches[0].Groups["launchParams"].Value.Split(',');
                
                return new OauthLoginResult
                {
                    SessionId = launchParams[1],
                    Region = int.Parse(launchParams[5]),
                    TermsAccepted = launchParams[3] != "0",
                    Playable = launchParams[9] != "0",
                    MaxExpansion = int.Parse(launchParams[13])
                };
            }
        }

        private static string GetLocalGamever()
        {
            try
            {
                return File.ReadAllText(Path.Combine(Settings.GetGamePath(), "game", "ffxivgame.ver"));
            }
            catch (Exception exc)
            {
                throw new Exception("Could not get local game version.", exc);
            }
        }

        private static string GetFileHash(string file)
        {
            byte[] bytes = File.ReadAllBytes(file);

            var hash = new SHA1Managed().ComputeHash(bytes);
            string hashstring = string.Join("", hash.Select(b => b.ToString("x2")).ToArray());

            long length = new System.IO.FileInfo(file).Length;

            return length + "/" + hashstring;
        }

        public bool GetGateStatus()
        {
            try
            {
                using (WebClient client = new WebClient())
                {
                    client.Headers.Add("user-agent", _userAgent);

                    string reply = client.DownloadString("http://frontier.ffxiv.com/worldStatus/gate_status.json");

                    return Convert.ToBoolean(int.Parse(reply[10].ToString()));
                }
            }
            catch (Exception exc)
            {
                throw new Exception("Could not get gate status.", exc);
            }

        }

        private static string MakeComputerId()
        {
            var hashString = Environment.MachineName + Environment.UserName + Environment.OSVersion + Environment.ProcessorCount;

            using (var sha1 = HashAlgorithm.Create("SHA1"))
            {
                var bytes = new byte[5];

                Array.Copy(sha1.ComputeHash(Encoding.Unicode.GetBytes(hashString)), 0, bytes, 1, 4);

                var checkSum = (byte) -(bytes[1] + bytes[2] + bytes[3] + bytes[4]);
                bytes[0] = checkSum;

                return BitConverter.ToString(bytes).Replace("-","").ToLower();
            }
            
        }

        private static string GetUserAgent()
        {
            return String.Format(UserAgentTemplate, MakeComputerId());
        }

        private static void InitiateSslTrust()
        {
            //Change SSL checks so that all checks pass, squares gamever server does strange things
            ServicePointManager.ServerCertificateValidationCallback =
                new RemoteCertificateValidationCallback(
                    delegate
                    { return true; }
                );
        }
    }
}
