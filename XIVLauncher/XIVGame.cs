using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace XIVLauncher
{
    static class XIVGame
    {
        private static readonly string UserAgent = "SQEXAuthor/2.0.0(Windows 6.2; ja-jp; 45d19cc985)";
        private static readonly string[] FilesToHash =
        {
            "ffxivboot.exe", 
            "ffxivboot64.exe",
            "ffxivlauncher.exe",
            "ffxivlauncher64.exe",
            "ffxivupdater.exe",
            "ffxivupdater64.exe",
        };

        public static void Login(string username, string password, string otp)
        {
            var loginResult = OauthLogin(username, password, otp);

            if (!loginResult.Playable)
            {
                MessageBox.Show("This Square Enix account cannot play FINAL FANTASY XIV.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!loginResult.TermsAccepted)
            {
                MessageBox.Show("Please accept the FINAL FANTASY XIV Terms of Use in the official launcher.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Clamp the expansion level to what the account is allowed to access
            var expansionLevel = Math.Min(Math.Max(loginResult.MaxExpansion, 0), Settings.GetExpansionLevel());
            var lobbySessionId = GetLobbySessionId(loginResult);

            LaunchGame(lobbySessionId, loginResult.Region, expansionLevel);
        }

        private static void LaunchGame(string sessionId, int region, int expansionLevel)
        {
            try {
                Process ffxivgame = new Process();
                if (Settings.IsDX11()) { ffxivgame.StartInfo.FileName = Settings.GetGamePath() + "/game/ffxiv_dx11.exe"; } else { ffxivgame.StartInfo.FileName = Settings.GetGamePath() + "/game/ffxiv.exe"; }
                ffxivgame.StartInfo.Arguments = $"DEV.TestSID={sessionId} DEV.MaxEntitledExpansionID={expansionLevel} language={Settings.GetLanguage()} region={region}";
                ffxivgame.Start();
            }catch(Exception exc)
            {
                MessageBox.Show("Could not launch executable. Is your game path correct?\n\n" + exc, "Launch failed", MessageBoxButtons.OK);
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

        private static string GetLobbySessionId(OauthLoginResult loginResult)
        {
            using (WebClient client = new WebClient())
            {
                client.Headers.Add("X-Hash-Check", "enabled");
                client.Headers.Add("user-agent", UserAgent);
                client.Headers.Add("Referer", $"https://ffxiv-login.square-enix.com/oauth/ffxivarr/login/top?lng=en&rgn={loginResult.Region}");
                client.Headers.Add("Content-Type", "application/x-www-form-urlencoded");

                InitiateSslTrust();

                var url = "https://patch-gamever.ffxiv.com/http/win32/ffxivneo_release_game/" + GetLocalGamever() +
                          "/" + loginResult.SessionId;

                client.UploadString(url, GetBootVersionHash());

                if(client.ResponseHeaders.AllKeys.Contains("X-Patch-Unique-Id"))
                    return client.ResponseHeaders["X-Patch-Unique-Id"];
                
                throw new Exception("Could not validate game version.");
            }
        }


        private static string GetStored() //this is needed to be able to access the login site correctly
        {
            WebClient loginInfo = new WebClient();
            loginInfo.Headers.Add("user-agent", UserAgent);
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

        private static OauthLoginResult OauthLogin(string username, string password, string otp)
        {
            using (WebClient client = new WebClient())
            {
                client.Headers.Add("user-agent", UserAgent);
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

        public static bool GetGateStatus()
        {
            try
            {
                using (WebClient client = new WebClient())
                {
                    string reply = client.DownloadString("http://frontier.ffxiv.com/worldStatus/gate_status.json");

                    return Convert.ToBoolean(int.Parse(reply[10].ToString()));
                }
            }
            catch (Exception exc)
            {
                throw new Exception("Could not get gate status.", exc);
            }

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
