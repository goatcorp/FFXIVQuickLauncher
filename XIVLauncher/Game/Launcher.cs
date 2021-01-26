using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
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
using Serilog;
using SteamworksSharp;
using SteamworksSharp.Native;
using XIVLauncher.Cache;
using XIVLauncher.Encryption;
using XIVLauncher.Game.Patch;
using XIVLauncher.Game.Patch.PatchList;
using XIVLauncher.PatchInstaller;
using XIVLauncher.Settings;
using XIVLauncher.Windows;

namespace XIVLauncher.Game
{
    public class Launcher
    {
        private async void DoFakeHttpRequests()
        {
            using (var client = new WebClient())
            {
                client.Headers.Add("User-Agent", _userAgent);

                client.Headers.Add("Accept", "image/gif, image/jpeg, image/pjpeg, application/x-ms-application, application/xaml+xml, application/x-ms-xbap, */*");

                client.Headers.Add("Accept-Encoding", "gzip, deflate");
                client.Headers.Add("Accept-Language", "en-US,en;q=0.8,ja;q=0.6,de-DE;q=0.4,de;q=0.2");

                var lang = App.Settings.Language.GetValueOrDefault(ClientLanguage.English);

                client.DownloadString(GenerateFrontierReferer(lang));

                DownloadAsLauncher($"https://frontier.ffxiv.com/v2/world/status.json?_={Util.GetUnixMillis()}",
                    lang, "application/json, text/plain, */*");
                DownloadAsLauncher($"https://frontier.ffxiv.com/worldStatus/login_status.json?_={Util.GetUnixMillis()}",
                    lang, "application/json, text/plain, */*");
                DownloadAsLauncher($"https://frontier.ffxiv.com/worldStatus/login_status.json?_={Util.GetUnixMillis() + 50}",
                    lang, "application/json, text/plain, */*");
            }
        }

        // The user agent for frontier pages. {0} has to be replaced by a unique computer id and its checksum
        private const string USER_AGENT_TEMPLATE = "SQEXAuthor/2.0.0(Windows 6.2; ja-jp; {0})";
        private readonly string _userAgent = GenerateUserAgent();

        private const int STEAM_APP_ID = 39210;

        private static readonly string[] FilesToHash =
        {
            "ffxivboot.exe",
            "ffxivboot64.exe",
            "ffxivlauncher.exe",
            "ffxivlauncher64.exe",
            "ffxivupdater.exe",
            "ffxivupdater64.exe"
        };

        public enum LoginState
        {
            Unknown,
            Ok,
            NeedsPatchGame,
            NeedsPatchBoot,
            NoOAuth
        }

        public UniqueIdCache Cache = new UniqueIdCache();

        public class LoginResult
        {
            public LoginState State { get; set; }
            public PatchListEntry[] PendingPatches { get; set; }
            public OauthLoginResult OauthLogin { get; set; }
            public string UniqueId { get; set; }
        }

        public LoginResult Login(string userName, string password, string otp, bool isSteamServiceAccount, bool useCache, DirectoryInfo gamePath)
        {
            string uid;
            PatchListEntry[] pendingPatches = null;

            OauthLoginResult oauthLoginResult;

            LoginState loginState;

            Log.Information($"XivGame::Login(steamServiceAccount:{isSteamServiceAccount}, cache:{useCache})");

            if (!useCache || !Cache.HasValidCache(userName))
            {
                Log.Information("Cache is invalid or disabled, logging in normally.");

                try
                {
                    oauthLoginResult = OauthLogin(userName, password, otp, isSteamServiceAccount, 3);

                    Log.Information($"OAuth login successful - playable:{oauthLoginResult.Playable} terms:{oauthLoginResult.TermsAccepted} region:{oauthLoginResult.Region} expack:{oauthLoginResult.MaxExpansion}");
                }
                catch (Exception ex)
                {
                    Log.Information(ex, "OAuth login failed.");
                    
                    return new LoginResult
                    {
                        State = LoginState.NoOAuth
                    };
                }

                if (!oauthLoginResult.Playable)
                {
                    MessageBox.Show("This Square Enix account cannot play FINAL FANTASY XIV.\n\nIf you bought FINAL FANTASY XIV on Steam, make sure to check the \"Use Steam service account\" checkbox while logging in.\nIf Auto-Login is enabled, hold shift while starting to access settings.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }

                if (!oauthLoginResult.TermsAccepted)
                {
                    MessageBox.Show("Please accept the FINAL FANTASY XIV Terms of Use in the official launcher.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }

                (uid, loginState, pendingPatches) = Task.Run(() => RegisterSession(oauthLoginResult, gamePath)).Result;

                if (useCache)
                    Task.Run(() => Cache.AddCachedUid(userName, uid, oauthLoginResult.Region, oauthLoginResult.MaxExpansion))
                        .Wait();
            }
            else
            {
                Log.Information("Cached UID found, using instead.");
                var (cachedUid, region, expansionLevel) = Task.Run(() => Cache.GetCachedUid(userName)).Result;
                uid = cachedUid;
                loginState = LoginState.Ok;

                oauthLoginResult = new OauthLoginResult
                {
                    Playable = true,
                    Region = region,
                    TermsAccepted = true,
                    MaxExpansion = expansionLevel
                };
            }

            return new LoginResult
            {
                PendingPatches = pendingPatches,
                OauthLogin = oauthLoginResult,
                State = loginState,
                UniqueId = uid
            };
        }

        public static Process LaunchGame(string sessionId, int region, int expansionLevel, bool isSteamIntegrationEnabled, bool isSteamServiceAccount, string additionalArguments, DirectoryInfo gamePath, bool isDx11, ClientLanguage language,
            bool encryptArguments)
        {
            Log.Information($"XivGame::LaunchGame(steamIntegration:{isSteamIntegrationEnabled}, steamServiceAccount:{isSteamServiceAccount}, args:{additionalArguments})");

            try
            {
                if (isSteamIntegrationEnabled)
                {
                    try
                    {
                        SteamNative.Initialize();

                        if (SteamApi.IsSteamRunning() && SteamApi.Initialize(STEAM_APP_ID))
                            Log.Information("Steam initialized.");
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Could not initialize Steam.");
                    }
                }

                var exePath = gamePath + "/game/ffxiv_dx11.exe";
                if (!isDx11)
                    exePath = gamePath + "/game/ffxiv.exe";

                var environment = new Dictionary<string, string>();

                var argumentBuilder = new ArgumentBuilder()
                    .Append("DEV.DataPathType", "1")
                    .Append("DEV.MaxEntitledExpansionID", expansionLevel.ToString())
                    .Append("DEV.TestSID", sessionId)
                    .Append("DEV.UseSqPack", "1")
                    .Append("SYS.Region", region.ToString())
                    .Append("language", ((int)language).ToString())
                    .Append("ver", Repository.Ffxiv.GetVer(gamePath));

                if (isSteamServiceAccount)
                {
                    // These environment variable and arguments seems to be set when ffxivboot is started with "-issteam" (27.08.2019)
                    environment.Add("IS_FFXIV_LAUNCH_FROM_STEAM", "1");
                    argumentBuilder.Append("IsSteam", "1");
                }

                // This is a bit of a hack; ideally additionalArguments would be a dictionary or some KeyValue structure
                if (!string.IsNullOrEmpty(additionalArguments))
                {
                    var regex = new Regex(@"\s*(?<key>[^=]+)\s*=\s*(?<value>[^\s]+)\s*", RegexOptions.Compiled);
                    foreach (Match match in regex.Matches(additionalArguments))
                        argumentBuilder.Append(match.Groups["key"].Value, match.Groups["value"].Value);
                }


                var workingDir = Path.Combine(gamePath.FullName, "game");

                Process game;
                try
                {
                    var arguments = encryptArguments
                        ? argumentBuilder.BuildEncrypted()
                        : argumentBuilder.Build();
                    game = NativeAclFix.LaunchGame(workingDir, exePath, arguments, environment);
                }
                catch (Win32Exception ex)
                {
                    MessageBox.Show(
                        "Could not start the game correctly. Please report this error.", "XIVLauncher Error", MessageBoxButton.OK, MessageBoxImage.Error);

                    Log.Error(ex, $"NativeLauncher error; {ex.NativeErrorCode}: {ex.Message}");

                    return null;
                }

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

        private static string GetVersionReport(DirectoryInfo gamePath, int exLevel)
        {
            var verReport = $"{GetBootVersionHash(gamePath)}";

            if (exLevel >= 1)
                verReport += $"\nex1\t{Repository.Ex1.GetVer(gamePath)}";

            if (exLevel >= 2)
                verReport += $"\nex2\t{Repository.Ex2.GetVer(gamePath)}";

            if (exLevel >= 3)
                verReport += $"\nex3\t{Repository.Ex3.GetVer(gamePath)}";

            return verReport;
        }

        /// <summary>
        /// Calculate the hash that is sent to patch-gamever for version verification/tamper protection.
        /// This same hash is also sent in lobby, but for ffxiv.exe and ffxiv_dx11.exe.
        /// </summary>
        /// <returns>String of hashed EXE files.</returns>
        private static string GetBootVersionHash(DirectoryInfo gamePath)
        {
            var result = Repository.Boot.GetVer(gamePath) + "=";

            for (var i = 0; i < FilesToHash.Length; i++)
            {
                result +=
                    $"{FilesToHash[i]}/{GetFileHash(Path.Combine(gamePath.FullName, "boot", FilesToHash[i]))}";

                if (i != FilesToHash.Length - 1)
                    result += ",";
            }

            return result;
        }

        public PatchListEntry[] CheckBootVersion(DirectoryInfo gamePath)
        {
            using var client = new WebClient();

            client.Headers.Add("User-Agent", "FFXIV PATCH CLIENT");
            client.Headers.Add("Host", "patch-bootver.ffxiv.com");

            // Why tf is this http??
            var url =
                $"http://patch-bootver.ffxiv.com/http/win32/ffxivneo_release_boot/{Repository.Boot.GetVer(gamePath)}/?time=" + GetLauncherFormattedTimeLong();

            var result = client.DownloadString(url);

            if (result == string.Empty)
                return null;

            Log.Verbose("BOOT Patching is needed... List:\n" + result);

            return PatchListParser.Parse(result);
        }

        private static (string Uid, LoginState result, PatchListEntry[] PendingGamePatches) RegisterSession(OauthLoginResult loginResult, DirectoryInfo gamePath)
        {
            using var client = new WebClient();

            client.Headers.Add("X-Hash-Check", "enabled");
            client.Headers.Add("User-Agent", "FFXIV PATCH CLIENT");
            //client.Headers.Add("Referer",
            //    $"https://ffxiv-login.square-enix.com/oauth/ffxivarr/login/top?lng=en&rgn={loginResult.Region}");
            client.Headers.Add("Content-Type", "application/x-www-form-urlencoded");

            var url =
                $"https://patch-gamever.ffxiv.com/http/win32/ffxivneo_release_game/{Repository.Ffxiv.GetVer(gamePath)}/{loginResult.SessionId}";

            try
            {
                var report = GetVersionReport(gamePath, loginResult.MaxExpansion);
                var result = client.UploadString(url, report);

                // Get the unique ID needed to authenticate with the lobby server
                if (client.ResponseHeaders.AllKeys.Contains("X-Patch-Unique-Id"))
                {
                    var sid = client.ResponseHeaders["X-Patch-Unique-Id"];

                    if (result == string.Empty)
                        return (sid, LoginState.Ok, null);

                    Log.Verbose("Patching is needed... List:\n" + result);

                    var pendingPatches = PatchListParser.Parse(result);

                    return (sid, LoginState.NeedsPatchGame, pendingPatches);
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
                            return (null, LoginState.NeedsPatchBoot, null);
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

        private string GetStored(bool isSteam, int region)
        {
            // This is needed to be able to access the login site correctly
            using var client = new WebClient();

            client.Headers.Add("Accept", "image/gif, image/jpeg, image/pjpeg, application/x-ms-application, application/xaml+xml, application/x-ms-xbap, */*");
            client.Headers.Add("Accept-Encoding", "gzip, deflate");
            client.Headers.Add("Accept-Language", "en-US,en;q=0.8,ja;q=0.6,de-DE;q=0.4,de;q=0.2");
            client.Headers.Add("User-Agent", _userAgent);
            var reply = client.DownloadString(
                $"https://ffxiv-login.square-enix.com/oauth/ffxivarr/login/top?lng=en&rgn={region}&isft=0&cssmode=1&isnew=1&issteam=" + (isSteam ? "1" : "0"));

            var regex = new Regex(@"\t<\s*input .* name=""_STORED_"" value=""(?<stored>.*)"">");
            return regex.Matches(reply)[0].Groups["stored"].Value;
        }

        public class OauthLoginResult
        {
            public string SessionId { get; set; }
            public int Region { get; set; }
            public bool TermsAccepted { get; set; }
            public bool Playable { get; set; }
            public int MaxExpansion { get; set; }
        }

        private OauthLoginResult OauthLogin(string userName, string password, string otp, bool isSteam, int region)
        {
            using var client = new WebClient();

            client.Headers.Add("User-Agent", _userAgent);
            client.Headers.Add("Cache-Control", "no-cache");
            client.Headers.Add("Accept", "image/gif, image/jpeg, image/pjpeg, application/x-ms-application, application/xaml+xml, application/x-ms-xbap, */*");
            client.Headers.Add("Accept-Encoding", "gzip, deflate");
            client.Headers.Add("Accept-Language", "en-US,en;q=0.8,ja;q=0.6,de-DE;q=0.4,de;q=0.2");
            client.Headers.Add("Referer",
                $"https://ffxiv-login.square-enix.com/oauth/ffxivarr/login/top?lng=en&rgn={region}&isft=0&cssmode=1&isnew=1&issteam=" + (isSteam ? "1" : "0"));
            client.Headers.Add("Content-Type", "application/x-www-form-urlencoded");

            var response =
                client.UploadValues("https://ffxiv-login.square-enix.com/oauth/ffxivarr/login/login.send",
                    new NameValueCollection //get the session id with user credentials
                    {
                        {"_STORED_", GetStored(isSteam, region)},
                        {"sqexid", userName},
                        {"password", password},
                        {"otppw", otp}
                    });

            var reply = Encoding.UTF8.GetString(response);

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
                        $"https://frontier.ffxiv.com/worldStatus/gate_status.json?{Util.GetUnixMillis()}", ClientLanguage.English));

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

            using var sha1 = HashAlgorithm.Create("SHA1");

            var bytes = new byte[5];

            Array.Copy(sha1.ComputeHash(Encoding.Unicode.GetBytes(hashString)), 0, bytes, 1, 4);

            var checkSum = (byte) -(bytes[1] + bytes[2] + bytes[3] + bytes[4]);
            bytes[0] = checkSum;

            return BitConverter.ToString(bytes).Replace("-", "").ToLower();
        }

        public byte[] DownloadAsLauncher(string url, ClientLanguage language, string contentType = "")
        {
            using var client = new WebClient();

            client.Headers.Add("User-Agent", _userAgent);

            if (!string.IsNullOrEmpty(contentType))
            {
                client.Headers.Add("Accept", contentType);
            }

            client.Headers.Add("Accept-Encoding", "gzip, deflate");
            client.Headers.Add("Accept-Language", "en-US,en;q=0.8,ja;q=0.6,de-DE;q=0.4,de;q=0.2");

            client.Headers.Add("Origin", "https://launcher.finalfantasyxiv.com");

            client.Headers.Add(HttpRequestHeader.Referer, GenerateFrontierReferer(language));

            return client.DownloadData(url);
        }

        private static string GenerateFrontierReferer(ClientLanguage language)
        {
            var langCode = language.GetLangCode();
            var formattedTime = GetLauncherFormattedTime();

            return $"https://frontier.ffxiv.com/version_5_0_win/index.html?rc_lang={langCode}&time={formattedTime}";
        }

        private static string GetLauncherFormattedTime() => DateTime.UtcNow.ToString("yyyy-MM-dd-HH");

        private static string GetLauncherFormattedTimeLong() => DateTime.UtcNow.ToString("yyyy-MM-dd-HH-mm");

        private static string GenerateUserAgent()
        {
            return string.Format(USER_AGENT_TEMPLATE, MakeComputerId());
        }
    }
}