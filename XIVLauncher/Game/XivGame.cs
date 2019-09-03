using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Nhaama.Memory;
using Serilog;
using SteamworksSharp;
using SteamworksSharp.Native;
using XIVLauncher.Cache;
using XIVLauncher.Windows;

namespace XIVLauncher.Game
{
    public class XivGame
    {
        // The user agent for frontier pages. {0} has to be replaced by a unique computer id and its checksum
        private static readonly string UserAgentTemplate = "SQEXAuthor/2.0.0(Windows 6.2; ja-jp; {0})";

        private readonly string _userAgent = GenerateUserAgent();

        private static readonly int SteamAppId = 39210;

        private static readonly string[] FilesToHash =
        {
            "ffxivboot.exe",
            "ffxivboot64.exe",
            "ffxivlauncher.exe",
            "ffxivlauncher64.exe",
            "ffxivupdater.exe",
            "ffxivupdater64.exe"
        };

        public UniqueIdCache Cache = new UniqueIdCache();

        public Process Login(string userName, string password, string otp, bool isSteamIntegrationEnabled, bool isSteamServiceAccount, string additionalArguments, bool useCache)
        {
            string uid;
            var needsUpdate = false;

            OauthLoginResult loginResult;

            Log.Information($"XivGame::Login(steamIntegration:{isSteamIntegrationEnabled}, steamServiceAccount:{isSteamServiceAccount}, args:{additionalArguments}, cache:{useCache})");

            if (!useCache || !Cache.HasValidCache(userName))
            {
                Log.Information("Cache is invalid or disabled, logging in normally.");

                try
                {
                    loginResult = OauthLogin(userName, password, otp, isSteamServiceAccount);

                    Log.Information($"OAuth login successful - playable:{loginResult.Playable} terms:{loginResult.TermsAccepted} region:{loginResult.Region} expack:{loginResult.MaxExpansion}");
                }
                catch (Exception ex)
                {
                    Log.Information(ex, "OAuth login failed.");
                    MessageBox.Show(
                        "Could not login into your Square Enix account.\nThis could be caused by bad credentials or OTPs.\n\nPlease also check your email inbox for any messages from Square Enix - they might want you to reset your password due to \"suspicious activity\".\nThis is NOT caused by a security issue in XIVLauncher, it is merely a safety measure by Square Enix to prevent logins from new locations, in case your account is getting stolen.\nXIVLauncher and the official launcher will work fine again after resetting your password.",
                        "Login issue", MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }

                if (!loginResult.Playable)
                {
                    MessageBox.Show("This Square Enix account cannot play FINAL FANTASY XIV.\n\nIf you bought FINAL FANTASY XIV on Steam, make sure to check the \"Use Steam service account\" checkbox while logging in.\nIf Auto-Login is enabled, hold shift while starting to access settings.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }

                if (!loginResult.TermsAccepted)
                {
                    MessageBox.Show("Please accept the FINAL FANTASY XIV Terms of Use in the official launcher.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }

                (uid, needsUpdate) = Task.Run(() => RegisterSession(loginResult)).Result;

                if (useCache)
                    Task.Run(() => Cache.AddCachedUid(userName, uid, loginResult.Region, loginResult.MaxExpansion))
                        .Wait();
            }
            else
            {
                Log.Information("Cached UID found, using instead.");
                var (cachedUid, region, expansionLevel) = Task.Run(() => Cache.GetCachedUid(userName)).Result;
                uid = cachedUid;

                loginResult = new OauthLoginResult
                {
                    Playable = true,
                    Region = region,
                    TermsAccepted = true,
                    MaxExpansion = expansionLevel
                };
            }

            if (needsUpdate)
            {
                MessageBox.Show(
                    "Your game is out of date. Please start the official launcher and update it before trying to log in.",
                    "Out of date", MessageBoxButton.OK, MessageBoxImage.Error);

                return null;
            }

            return LaunchGame(uid, loginResult.Region, loginResult.MaxExpansion, isSteamIntegrationEnabled, isSteamServiceAccount, additionalArguments);
        }

        private static Process LaunchGame(string sessionId, int region, int expansionLevel, bool isSteamIntegrationEnabled, bool isSteamServiceAccount, string additionalArguments,
            bool closeMutants = false)
        {
            try
            {
                if (isSteamIntegrationEnabled)
                {
                    try
                    {
                        SteamNative.Initialize();

                        if (SteamApi.IsSteamRunning() && SteamApi.Initialize(SteamAppId))
                            Log.Information("Steam initialized.");
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Could not initialize Steam.");
                    }
                }

                var game = new Process {StartInfo =
                {
                    UseShellExecute = false,
                    RedirectStandardError = false,
                    RedirectStandardInput = false,
                    RedirectStandardOutput = false
                }};

                if (Settings.IsDX11())
                    game.StartInfo.FileName = Settings.GamePath + "/game/ffxiv_dx11.exe";
                else
                    game.StartInfo.FileName = Settings.GamePath + "/game/ffxiv.exe";

                game.StartInfo.Arguments =
                    $"DEV.DataPathType=1 DEV.MaxEntitledExpansionID={expansionLevel} DEV.TestSID={sessionId} DEV.UseSqPack=1 SYS.Region={region} language={(int) Settings.GetLanguage()} ver={GetLocalGameVer()}";
                game.StartInfo.Arguments += " " + additionalArguments;

                if (isSteamServiceAccount)
                {
                    // These environment variable and arguments seems to be set when ffxivboot is started with "-issteam" (27.08.2019)
                    game.StartInfo.Environment.Add("IS_FFXIV_LAUNCH_FROM_STEAM", "1");
                    game.StartInfo.Arguments += " IsSteam=1";
                }

                /*
                var ticks = (uint) Environment.TickCount;
                var key = ticks & 0xFFF0_0000;

                var argumentBuilder = new ArgumentBuilder()
                    .Append("T", ticks.ToString())
                    .Append("DEV.DataPathType", "1")
                    .Append("DEV.MaxEntitledExpansionID", expansionLevel.ToString())
                    .Append("DEV.TestSID", sessionId)
                    .Append("DEV.UseSqPack", "1")
                    .Append("SYS.Region", region.ToString())
                    .Append("language", ((int) Settings.GetLanguage()).ToString())
                    .Append("ver", GetLocalGameVer());

                game.StartInfo.Arguments = argumentBuilder.BuildEncrypted(key);
                */

                game.StartInfo.WorkingDirectory = Path.Combine(Settings.GamePath.FullName, "game");

                game.Start();

                if (isSteamIntegrationEnabled)
                {
                    try
                    {
                        SteamApi.Uninitialize();
                        SteamNative.Uninitialize();
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Could not uninitialize Steam.");
                    }
                }

                for (var tries = 0; tries < 30; tries++)
                {
                    game.Refresh();

                    // Something went wrong here, why even bother
                    if (game.HasExited)
                        throw new Exception("Game exited prematurely");

                    // Is the main window open? Let's wait so any addons won't run into nothing
                    if (game.MainWindowHandle == IntPtr.Zero)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (closeMutants)
                        CloseMutants(game);
                    break;
                }

                return game;
            }
            catch (Exception ex)
            {
                new ErrorWindow(ex, "Your game path might not be correct. Please check in the settings.",
                    "XG LaunchGame").ShowDialog();
            }

            return null;
        }

        private static void CloseMutants(Process process)
        {
            var nhaamaProcess = process.GetNhaamaProcess();

            var handles = nhaamaProcess.GetHandles();

            // Check if handle is a ffxiv mutant and close it
            foreach (var nhaamaHandle in handles)
                if (nhaamaHandle.Name.Contains("ffxiv_game0"))
                    nhaamaHandle.Close();
        }

        /// <summary>
        /// Calculate the hash that is sent to patch-gamever for version verification/tamper protection.
        /// This same hash is also sent in lobby, but for ffxiv.exe and ffxiv_dx11.exe.
        /// </summary>
        /// <returns>String of hashed EXE files.</returns>
        private static string GetBootVersionHash()
        {
            var result = "";

            for (var i = 0; i < FilesToHash.Length; i++)
            {
                result +=
                    $"{FilesToHash[i]}/{GetFileHash(Path.Combine(Settings.GamePath.FullName, "boot", FilesToHash[i]))}";

                if (i != FilesToHash.Length - 1)
                    result += ",";
            }

            return result;
        }

        private static (string Uid, bool NeedsUpdate) RegisterSession(OauthLoginResult loginResult)
        {
            using (var client = new WebClient())
            {
                client.Headers.Add("X-Hash-Check", "enabled");
                client.Headers.Add("User-Agent", "FFXIV PATCH CLIENT");
                client.Headers.Add("Referer",
                    $"https://ffxiv-login.square-enix.com/oauth/ffxivarr/login/top?lng=en&rgn={loginResult.Region}");
                client.Headers.Add("Content-Type", "application/x-www-form-urlencoded");

                var url =
                    $"https://patch-gamever.ffxiv.com/http/win32/ffxivneo_release_game/{GetLocalGameVer()}/{loginResult.SessionId}";

                try
                {
                    var result = client.UploadString(url, GetBootVersionHash());

                    // Get the unique ID needed to authenticate with the lobby server
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
                        if (exc.Response is HttpWebResponse response)
                        {
                            // Conflict indicates that boot needs to update, we do not get a patch list or a unique ID to download patches with in this case
                            if (response.StatusCode == HttpStatusCode.Conflict)
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

        private string GetStored(bool isSteam)
        {
            // This is needed to be able to access the login site correctly
            using (var client = new WebClient())
            {
                client.Headers.Add("User-Agent", _userAgent);
                var reply = client.DownloadString(
                    "https://ffxiv-login.square-enix.com/oauth/ffxivarr/login/top?lng=en&rgn=3&isft=0&issteam=" + (isSteam ? "1" : "0"));

                var regex = new Regex(@"\t<\s*input .* name=""_STORED_"" value=""(?<stored>.*)"">");
                return regex.Matches(reply)[0].Groups["stored"].Value;
            }
        }

        internal class OauthLoginResult
        {
            public string SessionId { get; set; }
            public int Region { get; set; }
            public bool TermsAccepted { get; set; }
            public bool Playable { get; set; }
            public int MaxExpansion { get; set; }
        }

        private OauthLoginResult OauthLogin(string userName, string password, string otp, bool isSteam)
        {
            using (var client = new WebClient())
            {
                client.Headers.Add("User-Agent", _userAgent);
                client.Headers.Add("Referer",
                    "https://ffxiv-login.square-enix.com/oauth/ffxivarr/login/top?lng=en&rgn=3&isft=0&issteam=" + (isSteam ? "1" : "0"));
                client.Headers.Add("Content-Type", "application/x-www-form-urlencoded");

                var response =
                    client.UploadValues("https://ffxiv-login.square-enix.com/oauth/ffxivarr/login/login.send",
                        new NameValueCollection //get the session id with user credentials
                        {
                            {"_STORED_", GetStored(isSteam)},
                            {"sqexid", userName},
                            {"password", password},
                            {"otppw", otp}
                        });

                var reply = Encoding.UTF8.GetString(response);

                Log.Information(reply);

                var regex = new Regex(@"window.external.user\(""login=auth,ok,(?<launchParams>.*)\);");
                var matches = regex.Matches(reply);

                if (matches.Count == 0)
                    throw new OauthLoginException("Could not log in to oauth. Result: " + reply);

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

        public static string GetLocalGameVer()
        {
            try
            {
                return File.ReadAllText(Path.Combine(Settings.GamePath.FullName, "game", "ffxivgame.ver"));
            }
            catch (Exception exc)
            {
                throw new Exception("Could not get local game version.", exc);
            }
        }

        public static string GetLocalBootVer()
        {
            try
            {
                return File.ReadAllText(Path.Combine(Settings.GamePath.FullName, "boot", "ffxivboot.ver"));
            }
            catch (Exception exc)
            {
                throw new Exception("Could not get local boot version.", exc);
            }
        }

        private static string GetFileHash(string file)
        {
            var bytes = File.ReadAllBytes(file);

            var hash = new SHA1Managed().ComputeHash(bytes);
            var hashstring = string.Join("", hash.Select(b => b.ToString("x2")).ToArray());

            var length = new FileInfo(file).Length;

            return length + "/" + hashstring;
        }

        public bool GetGateStatus()
        {
            try
            {
                var reply = Encoding.UTF8.GetString(
                    DownloadAsLauncher(
                        $"https://frontier.ffxiv.com/worldStatus/gate_status.json?{Util.GetUnixMillis()}"));

                return Convert.ToBoolean(int.Parse(reply[10].ToString()));
            }
            catch (Exception exc)
            {
                throw new Exception("Could not get gate status.", exc);
            }
        }

        private static string MakeComputerId()
        {
            var hashString = Environment.MachineName + Environment.UserName + Environment.OSVersion +
                             Environment.ProcessorCount;

            using (var sha1 = HashAlgorithm.Create("SHA1"))
            {
                var bytes = new byte[5];

                Array.Copy(sha1.ComputeHash(Encoding.Unicode.GetBytes(hashString)), 0, bytes, 1, 4);

                var checkSum = (byte) -(bytes[1] + bytes[2] + bytes[3] + bytes[4]);
                bytes[0] = checkSum;

                return BitConverter.ToString(bytes).Replace("-", "").ToLower();
            }
        }

        public byte[] DownloadAsLauncher(string url)
        {
            using (var client = new WebClient())
            {
                client.Headers.Add("User-Agent", _userAgent);
                client.Headers.Add(HttpRequestHeader.Referer, GenerateFrontierReferer());

                return client.DownloadData(url);
            }
        }

        private static string GenerateFrontierReferer()
        {
            var langCode = Settings.GetLanguage().GetLangCode();
            var formattedTime = DateTime.UtcNow.ToString("yyyy-MM-dd-HH");

            return $"https://frontier.ffxiv.com/version_5_0_win/index.html?rc_lang={langCode}&time={formattedTime}";
        }

        private static string GenerateUserAgent()
        {
            return string.Format(UserAgentTemplate, MakeComputerId());
        }
    }
}