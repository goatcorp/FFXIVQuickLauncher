using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CheapLoc;
using Serilog;
using SteamworksSharp;
using SteamworksSharp.Native;
using XIVLauncher.Cache;
using XIVLauncher.Encryption;
using XIVLauncher.Game.Patch.PatchList;
using XIVLauncher.PatchInstaller;
using XIVLauncher.Windows;

namespace XIVLauncher.Game
{
    public class Launcher
    {
        public Launcher()
        {
            ServicePointManager.Expect100Continue = false;
            var handler = new HttpClientHandler
            {
                UseCookies = false,
            };
            _client = new HttpClient(handler);
        }

        // The user agent for frontier pages. {0} has to be replaced by a unique computer id and its checksum
        private const string USER_AGENT_TEMPLATE = "SQEXAuthor/2.0.0(Windows 6.2; ja-jp; {0})";
        private readonly string _userAgent = GenerateUserAgent();

        private const int STEAM_APP_ID = 39210;

        private readonly HttpClient _client;

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
            NoOAuth,
            NoService,
            NoTerms
        }

        public class LoginResult
        {
            public LoginState State { get; set; }
            public PatchListEntry[] PendingPatches { get; set; }
            public OauthLoginResult OauthLogin { get; set; }
            public string UniqueId { get; set; }
        }

        public async Task<LoginResult> Login(string userName, string password, string otp, bool isSteamServiceAccount, bool useCache, DirectoryInfo gamePath)
        {
            string uid;
            PatchListEntry[] pendingPatches = null;

            OauthLoginResult oauthLoginResult;

            LoginState loginState;

            Log.Information($"XivGame::Login(steamServiceAccount:{isSteamServiceAccount}, cache:{useCache})");

            if (!useCache || !UniqueIdCache.Instance.HasValidCache(userName))
            {
                Log.Information("Cache is invalid or disabled, logging in normally.");

                oauthLoginResult = await OauthLogin(userName, password, otp, isSteamServiceAccount, 3);

                Log.Information($"OAuth login successful - playable:{oauthLoginResult.Playable} terms:{oauthLoginResult.TermsAccepted} region:{oauthLoginResult.Region} expack:{oauthLoginResult.MaxExpansion}");

                if (!oauthLoginResult.Playable)
                {
                    return new LoginResult
                    {
                        State = LoginState.NoService
                    };
                }

                if (!oauthLoginResult.TermsAccepted)
                {
                    return new LoginResult
                    {
                        State = LoginState.NoTerms
                    };
                }

                (uid, loginState, pendingPatches) = await RegisterSession(oauthLoginResult, gamePath);

                if (useCache)
                    Task.Run(() => UniqueIdCache.Instance.AddCachedUid(userName, uid, oauthLoginResult.Region, oauthLoginResult.MaxExpansion))
                        .Wait();
            }
            else
            {
                Log.Information("Cached UID found, using instead.");
                var (cachedUid, region, expansionLevel) = Task.Run(() => UniqueIdCache.Instance.GetCachedUid(userName)).Result;
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

                var exePath = Path.Combine(gamePath.FullName, "game", "ffxiv_dx11.exe");
                if (!isDx11)
                    exePath = Path.Combine(gamePath.FullName, "game", "ffxiv.exe");

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

                if (!File.Exists(exePath))
                {
                    CustomMessageBox.Show(
                        Loc.Localize("BinaryNotPresentError", "Could not find the game executable.\nThis might be caused by your antivirus. You may have to reinstall the game."), "XIVLauncher Error", MessageBoxButton.OK, MessageBoxImage.Error);

                    Log.Error("Game binary at {0} wasn't present.", exePath);

                    return null;
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
                    CustomMessageBox.Show(
                        string.Format(Loc.Localize("NativeLauncherError", "Could not start the game correctly. Please report this error.\n\nHRESULT: 0x{0}"), ex.HResult.ToString("X")), "XIVLauncher Error", MessageBoxButton.OK, MessageBoxImage.Error);

                    Log.Error(ex, $"NativeLauncher error; {ex.HResult}: {ex.Message}");

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
                    {
                        if (Process.GetProcessesByName("ffxiv_dx11").Length +
                            Process.GetProcessesByName("ffxiv").Length >= 2)
                        {
                            CustomMessageBox.Show(
                                Loc.Localize("MultiboxDeniedWarning",
                                    "You can't launch more than two instances of the game by default.\n\nPlease check if there is an instance of the game that did not close correctly."),
                                "XIVLauncher Error", image: MessageBoxImage.Error);

                            return null;
                        }
                        else
                        {
                            throw new Exception("Game exited prematurely");
                        }
                    }

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

            if (exLevel >= 4)
                verReport += $"\nex4\t{Repository.Ex4.GetVer(gamePath)}";

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

        public async Task<PatchListEntry[]> CheckBootVersion(DirectoryInfo gamePath)
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
                $"http://patch-bootver.ffxiv.com/http/win32/ffxivneo_release_boot/{Repository.Boot.GetVer(gamePath)}/?time=" +
                GetLauncherFormattedTimeLong());

            request.Headers.AddWithoutValidation("User-Agent", "FFXIV PATCH CLIENT");
            request.Headers.AddWithoutValidation("Host", "patch-bootver.ffxiv.com");

            var resp = await _client.SendAsync(request);
            var text = await resp.Content.ReadAsStringAsync();

            if (text == string.Empty)
                return null;

            Log.Verbose("Boot patching is needed... List:\n" + resp);

            return PatchListParser.Parse(text);
        }

        private async Task<(string Uid, LoginState result, PatchListEntry[] PendingGamePatches)> RegisterSession(OauthLoginResult loginResult, DirectoryInfo gamePath)
        {
            var request = new HttpRequestMessage(HttpMethod.Post,
                $"https://patch-gamever.ffxiv.com/http/win32/ffxivneo_release_game/{Repository.Ffxiv.GetVer(gamePath)}/{loginResult.SessionId}");

            request.Headers.AddWithoutValidation("X-Hash-Check", "enabled");
            request.Headers.AddWithoutValidation("User-Agent", "FFXIV PATCH CLIENT");
            //client.Headers.AddWithoutValidation("Referer",
            //    $"https://ffxiv-login.square-enix.com/oauth/ffxivarr/login/top?lng=en&rgn={loginResult.Region}");
            //request.Headers.AddWithoutValidation("Content-Type", "application/x-www-form-urlencoded");

            request.Content = new StringContent(GetVersionReport(gamePath, loginResult.MaxExpansion));

            var resp = await _client.SendAsync(request);
            var text = await resp.Content.ReadAsStringAsync();

            // Conflict indicates that boot needs to update, we do not get a patch list or a unique ID to download patches with in this case
            if (resp.StatusCode == HttpStatusCode.Conflict)
                return (null, LoginState.NeedsPatchBoot, null);

            if (!resp.Headers.TryGetValues("X-Patch-Unique-Id", out var uidVals))
                throw new Exception("Could not validate game version.");

            var uid = uidVals.First();

            if (string.IsNullOrEmpty(text))
                return (uid, LoginState.Ok, null);

            Log.Verbose("Game Patching is needed... List:\n" + text);

            var pendingPatches = PatchListParser.Parse(text);
            return (uid, LoginState.NeedsPatchGame, pendingPatches);
        }

        private async Task<string> GetStored(bool isSteam, int region)
        {
            // This is needed to be able to access the login site correctly
            var request = new HttpRequestMessage(HttpMethod.Get, $"https://ffxiv-login.square-enix.com/oauth/ffxivarr/login/top?lng=en&rgn={region}&isft=0&cssmode=1&isnew=1&issteam=" + (isSteam ? "1" : "0"));
            request.Headers.AddWithoutValidation("Accept", "image/gif, image/jpeg, image/pjpeg, application/x-ms-application, application/xaml+xml, application/x-ms-xbap, */*");
            request.Headers.AddWithoutValidation("Referer", GenerateFrontierReferer(App.Settings.Language.GetValueOrDefault(ClientLanguage.English)));
            request.Headers.AddWithoutValidation("Accept-Encoding", "gzip, deflate");
            request.Headers.AddWithoutValidation("Accept-Language", App.Settings.AcceptLanguage);
            request.Headers.AddWithoutValidation("User-Agent", _userAgent);
            request.Headers.AddWithoutValidation("Connection", "Keep-Alive");
            request.Headers.AddWithoutValidation("Cookie", "_rsid=\"\"");

            var reply = await _client.SendAsync(request);
            var text = await reply.Content.ReadAsStringAsync();

            var regex = new Regex(@"\t<\s*input .* name=""_STORED_"" value=""(?<stored>.*)"">");
            return regex.Matches(text)[0].Groups["stored"].Value;
        }

        public class OauthLoginResult
        {
            public string SessionId { get; set; }
            public int Region { get; set; }
            public bool TermsAccepted { get; set; }
            public bool Playable { get; set; }
            public int MaxExpansion { get; set; }
        }

        private async Task<OauthLoginResult> OauthLogin(string userName, string password, string otp, bool isSteam, int region)
        {
            var request = new HttpRequestMessage(HttpMethod.Post,
                "https://ffxiv-login.square-enix.com/oauth/ffxivarr/login/login.send");

            request.Headers.AddWithoutValidation("Accept", "image/gif, image/jpeg, image/pjpeg, application/x-ms-application, application/xaml+xml, application/x-ms-xbap, */*");
            request.Headers.AddWithoutValidation("Referer",
                $"https://ffxiv-login.square-enix.com/oauth/ffxivarr/login/top?lng=en&rgn={region}&isft=0&cssmode=1&isnew=1&issteam=" + (isSteam ? "1" : "0"));
            request.Headers.AddWithoutValidation("Accept-Language", App.Settings.AcceptLanguage);
            request.Headers.AddWithoutValidation("User-Agent", _userAgent);
            //request.Headers.AddWithoutValidation("Content-Type", "application/x-www-form-urlencoded");
            request.Headers.AddWithoutValidation("Accept-Encoding", "gzip, deflate");
            request.Headers.AddWithoutValidation("Host", "ffxiv-login.square-enix.com");
            request.Headers.AddWithoutValidation("Connection", "Keep-Alive");
            request.Headers.AddWithoutValidation("Cache-Control", "no-cache");
            request.Headers.AddWithoutValidation("Cookie", "_rsid=\"\"");

            request.Content = new FormUrlEncodedContent(
                new Dictionary<string, string>()
                {
                    { "_STORED_", await GetStored(isSteam, region) },
                    { "sqexid", userName },
                    { "password", password },
                    { "otppw", otp }
                });

            var response = await _client.SendAsync(request);

            var reply = await response.Content.ReadAsStringAsync();

            var regex = new Regex(@"window.external.user\(""login=auth,ok,(?<launchParams>.*)\);");
            var matches = regex.Matches(reply);

            if (matches.Count == 0)
                throw new OauthLoginException(reply);

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

        public async Task<bool> GetGateStatus()
        {
            try
            {
                var reply = Encoding.UTF8.GetString(
                    await DownloadAsLauncher(
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

        public async Task<byte[]> DownloadAsLauncher(string url, ClientLanguage language, string contentType = "")
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);

            request.Headers.AddWithoutValidation("User-Agent", _userAgent);

            if (!string.IsNullOrEmpty(contentType))
            {
                request.Headers.AddWithoutValidation("Accept", contentType);
            }

            request.Headers.AddWithoutValidation("Accept-Encoding", "gzip, deflate");
            request.Headers.AddWithoutValidation("Accept-Language", App.Settings.AcceptLanguage);

            request.Headers.AddWithoutValidation("Origin", "https://launcher.finalfantasyxiv.com");

            request.Headers.AddWithoutValidation("Referer", GenerateFrontierReferer(language));

            var resp = await _client.SendAsync(request);
            return await resp.Content.ReadAsByteArrayAsync();
        }

        private static string GenerateFrontierReferer(ClientLanguage language)
        {
            var langCode = language.GetLangCode();
            var formattedTime = GetLauncherFormattedTime();

            return $"https://launcher.finalfantasyxiv.com/v550/index.html?rc_lang={langCode}&time={formattedTime}";
        }

        private static string GetLauncherFormattedTime() => DateTime.UtcNow.ToString("yyyy-MM-dd-HH");

        private static string GetLauncherFormattedTimeLong() => DateTime.UtcNow.ToString("yyyy-MM-dd-HH-mm");

        private static string GenerateUserAgent()
        {
            return string.Format(USER_AGENT_TEMPLATE, MakeComputerId());
        }
    }
}