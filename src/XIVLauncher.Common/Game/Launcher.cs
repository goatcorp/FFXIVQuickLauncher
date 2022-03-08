using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Serilog;
using XIVLauncher.Common.Game.Patch.PatchList;
using XIVLauncher.Common.Encryption;
using XIVLauncher.Common.PlatformAbstractions;

namespace XIVLauncher.Common.Game
{
    public class Launcher
    {
        private readonly ISteam steam;
        private readonly IUniqueIdCache uniqueIdCache;
        private readonly ISettings settings;
        private readonly HttpClient client;

        public Launcher(ISteam steam, IUniqueIdCache uniqueIdCache, ISettings settings)
        {
            this.steam = steam;
            this.uniqueIdCache = uniqueIdCache;
            this.settings = settings;

            ServicePointManager.Expect100Continue = false;
            var handler = new HttpClientHandler
            {
                UseCookies = false,
            };
            this.client = new HttpClient(handler);
        }

        // The user agent for frontier pages. {0} has to be replaced by a unique computer id and its checksum
        private const string USER_AGENT_TEMPLATE = "SQEXAuthor/2.0.0(Windows 6.2; ja-jp; {0})";
        private readonly string _userAgent = GenerateUserAgent();

        public const int STEAM_APP_ID = 39210;

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

        public async Task<LoginResult> Login(string userName, string password, string otp, bool isSteamServiceAccount, bool useCache, DirectoryInfo gamePath, bool forceBaseVersion)
        {
            string uid;
            PatchListEntry[] pendingPatches = null;

            OauthLoginResult oauthLoginResult;

            LoginState loginState;

            Log.Information("XivGame::Login(steamServiceAccount:{IsSteam}, cache:{UseCache})", isSteamServiceAccount, useCache);

            byte[] steamTicket = null;

            if (isSteamServiceAccount)
            {
                try
                {
                    this.steam.Initialize(STEAM_APP_ID);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Could not initialize Steam");
                    //throw new SteamException("SteamAPI_Init() failed.", ex);
                }

                if (!this.steam.IsValid)
                {
                    throw new SteamException("Not logged into Steam. Please log in and try again.");
                }

                try
                {
                    steamTicket = this.steam.GetAuthSessionTicket();
                }
                catch (Exception ex)
                {
                    throw new SteamException("Could not request encrypted app ticket.", ex);
                }

                if (steamTicket == null)
                {
                    throw new SteamException("Steam app ticket was null.");
                }
            }

            if (!useCache || !this.uniqueIdCache.TryGet(userName, out var cached))
            {
                oauthLoginResult = await OauthLogin(userName, password, otp, isSteamServiceAccount, 3, steamTicket);

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

                (uid, loginState, pendingPatches) = await RegisterSession(oauthLoginResult, gamePath, forceBaseVersion);

                if (useCache)
                    this.uniqueIdCache.Add(userName, uid, oauthLoginResult.Region, oauthLoginResult.MaxExpansion);
            }
            else
            {
                Log.Information("Cached UID found, using instead");
                uid = cached.UniqueId;
                loginState = LoginState.Ok;

                oauthLoginResult = new OauthLoginResult
                {
                    Playable = true,
                    Region = cached.Region,
                    TermsAccepted = true,
                    MaxExpansion = cached.MaxExpansion
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

        public Process LaunchGame(IGameRunner runner, string sessionId, int region, int expansionLevel,
                                  bool isSteamServiceAccount, string additionalArguments,
                                  DirectoryInfo gamePath, bool isDx11, ClientLanguage language,
                                  bool encryptArguments, DpiAwareness dpiAwareness)
        {
            Log.Information(
                $"XivGame::LaunchGame(steamServiceAccount:{isSteamServiceAccount}, args:{additionalArguments})");

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
                throw new BinaryNotPresentException(exePath);

            var workingDir = Path.Combine(gamePath.FullName, "game");

            var arguments = encryptArguments
                ? argumentBuilder.BuildEncrypted()
                : argumentBuilder.Build();

            var game = runner.Start(exePath, workingDir, arguments, environment, dpiAwareness);

            return game;
        }

        private static string GetVersionReport(DirectoryInfo gamePath, int exLevel, bool forceBaseVersion)
        {
            var verReport = $"{GetBootVersionHash(gamePath)}";

            if (exLevel >= 1)
                verReport += $"\nex1\t{(forceBaseVersion ? Constants.BASE_GAME_VERSION : Repository.Ex1.GetVer(gamePath))}";

            if (exLevel >= 2)
                verReport += $"\nex2\t{(forceBaseVersion ? Constants.BASE_GAME_VERSION : Repository.Ex2.GetVer(gamePath))}";

            if (exLevel >= 3)
                verReport += $"\nex3\t{(forceBaseVersion ? Constants.BASE_GAME_VERSION : Repository.Ex3.GetVer(gamePath))}";

            if (exLevel >= 4)
                verReport += $"\nex4\t{(forceBaseVersion ? Constants.BASE_GAME_VERSION : Repository.Ex4.GetVer(gamePath))}";

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

            request.Headers.AddWithoutValidation("User-Agent", Constants.PatcherUserAgent);
            request.Headers.AddWithoutValidation("Host", "patch-bootver.ffxiv.com");

            var resp = await this.client.SendAsync(request);
            var text = await resp.Content.ReadAsStringAsync();

            if (text == string.Empty)
                return null;

            Log.Verbose("Boot patching is needed... List:\n{PatchList}", resp);

            return PatchListParser.Parse(text);
        }

        private async Task<(string Uid, LoginState result, PatchListEntry[] PendingGamePatches)> RegisterSession(OauthLoginResult loginResult, DirectoryInfo gamePath, bool forceBaseVersion)
        {
            var request = new HttpRequestMessage(HttpMethod.Post,
                $"https://patch-gamever.ffxiv.com/http/win32/ffxivneo_release_game/{(forceBaseVersion ? Constants.BASE_GAME_VERSION : Repository.Ffxiv.GetVer(gamePath))}/{loginResult.SessionId}");

            request.Headers.AddWithoutValidation("X-Hash-Check", "enabled");
            request.Headers.AddWithoutValidation("User-Agent", Constants.PatcherUserAgent);

            request.Content = new StringContent(GetVersionReport(gamePath, loginResult.MaxExpansion, forceBaseVersion));

            var resp = await this.client.SendAsync(request);
            var text = await resp.Content.ReadAsStringAsync();

            // Conflict indicates that boot needs to update, we do not get a patch list or a unique ID to download patches with in this case
            if (resp.StatusCode == HttpStatusCode.Conflict)
                return (null, LoginState.NeedsPatchBoot, null);

            if (!resp.Headers.TryGetValues("X-Patch-Unique-Id", out var uidVals))
                throw new InvalidResponseException("Could not get X-Patch-Unique-Id.");

            var uid = uidVals.First();

            if (string.IsNullOrEmpty(text))
                return (uid, LoginState.Ok, null);

            Log.Verbose("Game Patching is needed... List:\n{PatchList}", text);

            var pendingPatches = PatchListParser.Parse(text);
            return (uid, LoginState.NeedsPatchGame, pendingPatches);
        }

        public async Task<string> GenPatchToken(string patchUrl, string uniqueId)
        {
            // Yes, Square does use HTTP for this and sends tokens in headers. IT'S NOT MY FAULT.
            var request = new HttpRequestMessage(HttpMethod.Post, "http://patch-gamever.ffxiv.com/gen_token");

            request.Headers.AddWithoutValidation("Connection", "Keep-Alive");
            request.Headers.AddWithoutValidation("X-Patch-Unique-Id", uniqueId);
            request.Headers.AddWithoutValidation("User-Agent", Constants.PatcherUserAgent);

            request.Content = new StringContent(patchUrl);

            var resp = await this.client.SendAsync(request);
            resp.EnsureSuccessStatusCode();

            return await resp.Content.ReadAsStringAsync();
        }

        private async Task<(string Stored, string? SteamLinkedId)> GetOauthTop(string url, bool isSteam)
        {
            // This is needed to be able to access the login site correctly
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.AddWithoutValidation("Accept", "image/gif, image/jpeg, image/pjpeg, application/x-ms-application, application/xaml+xml, application/x-ms-xbap, */*");
            request.Headers.AddWithoutValidation("Referer", GenerateFrontierReferer(this.settings.ClientLanguage.GetValueOrDefault(ClientLanguage.English)));
            request.Headers.AddWithoutValidation("Accept-Encoding", "gzip, deflate");
            request.Headers.AddWithoutValidation("Accept-Language", this.settings.AcceptLanguage);
            request.Headers.AddWithoutValidation("User-Agent", _userAgent);
            request.Headers.AddWithoutValidation("Connection", "Keep-Alive");
            request.Headers.AddWithoutValidation("Cookie", "_rsid=\"\"");

            var reply = await this.client.SendAsync(request);

            var text = await reply.Content.ReadAsStringAsync();

            if (reply.StatusCode == (HttpStatusCode)210)
                throw new SteamLinkNeededException();

            var storedRegex = new Regex(@"\t<\s*input .* name=""_STORED_"" value=""(?<stored>.*)"">");
            var matches = storedRegex.Matches(text);

            if (matches.Count == 0)
                throw new InvalidResponseException("Could not get STORED.");

            string? steamUsername = null;

            if (isSteam)
            {
                var steamRegex = new Regex(@"<input name=""sqexid"" type=""hidden"" value=""(?<sqexid>.*)""\/>");
                var steamMatches = storedRegex.Matches(text);

                if (steamMatches.Count == 0)
                    throw new InvalidResponseException("Could not get steam username.");

                steamUsername = steamMatches[0].Groups["sqexid"].Value;
            }

            return (matches[0].Groups["stored"].Value, steamUsername);
        }

        public class OauthLoginResult
        {
            public string SessionId { get; set; }
            public int Region { get; set; }
            public bool TermsAccepted { get; set; }
            public bool Playable { get; set; }
            public int MaxExpansion { get; set; }
        }

        private static string GetOauthTopUrl(int region, bool isSteam, byte[] steamTicket)
        {
            var url =
                $"https://ffxiv-login.square-enix.com/oauth/ffxivarr/login/top?lng=en&rgn={region}&isft=0&cssmode=1&isnew=1&launchver=3";

            if (isSteam)
            {
                url += "&issteam=1";

                var ticketText = Convert.ToBase64String(steamTicket);
                url += $"&session_ticket={ticketText}";
                url += $"&ticket_size={ticketText.Length}";
            }

            return url;
        }

        private async Task<OauthLoginResult> OauthLogin(string userName, string password, string otp, bool isSteam, int region, byte[]? steamTicket)
        {
            if (isSteam && steamTicket == null)
                throw new ArgumentNullException(nameof(steamTicket), "isSteam, but steamTicket == null");

            var topUrl = GetOauthTopUrl(region, isSteam, steamTicket);
            var topResult = await GetOauthTop(topUrl, isSteam);

            var request = new HttpRequestMessage(HttpMethod.Post,
                "https://ffxiv-login.square-enix.com/oauth/ffxivarr/login/login.send");

            request.Headers.AddWithoutValidation("Accept", "image/gif, image/jpeg, image/pjpeg, application/x-ms-application, application/xaml+xml, application/x-ms-xbap, */*");
            request.Headers.AddWithoutValidation("Referer", topUrl);
            request.Headers.AddWithoutValidation("Accept-Language", this.settings.AcceptLanguage);
            request.Headers.AddWithoutValidation("User-Agent", _userAgent);
            //request.Headers.AddWithoutValidation("Content-Type", "application/x-www-form-urlencoded");
            request.Headers.AddWithoutValidation("Accept-Encoding", "gzip, deflate");
            request.Headers.AddWithoutValidation("Host", "ffxiv-login.square-enix.com");
            request.Headers.AddWithoutValidation("Connection", "Keep-Alive");
            request.Headers.AddWithoutValidation("Cache-Control", "no-cache");
            request.Headers.AddWithoutValidation("Cookie", "_rsid=\"\"");

            if (isSteam && userName != topResult.SteamLinkedId)
                throw new SteamWrongAccountException();

            request.Content = new FormUrlEncodedContent(
                new Dictionary<string, string>()
                {
                    { "_STORED_", topResult.Stored },
                    { "sqexid", userName },
                    { "password", password },
                    { "otppw", otp },
                    // { "saveid", "1" } // NOTE(goat): This adds a Set-Cookie with a filled-out _rsid value in the login response.
                });

            var response = await this.client.SendAsync(request);

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
                        $"https://frontier.ffxiv.com/worldStatus/gate_status.json?{Util.GetUnixMillis()}", ClientLanguage.English).ConfigureAwait(true));

                return Convert.ToBoolean(int.Parse(reply[10].ToString()));
            }
            catch (Exception exc)
            {
                throw new Exception("Could not get gate status", exc);
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
            request.Headers.AddWithoutValidation("Accept-Language", this.settings.AcceptLanguage);

            request.Headers.AddWithoutValidation("Origin", "https://launcher.finalfantasyxiv.com");

            request.Headers.AddWithoutValidation("Referer", GenerateFrontierReferer(language));

            var resp = await this.client.SendAsync(request);
            return await resp.Content.ReadAsByteArrayAsync();
        }

        private static string GenerateFrontierReferer(ClientLanguage language)
        {
            var langCode = language.GetLangCode();
            var formattedTime = GetLauncherFormattedTime();

            return $"https://launcher.finalfantasyxiv.com/v600/index.html?rc_lang={langCode}&time={formattedTime}";
        }

        private static string GetLauncherFormattedTime() => DateTime.UtcNow.ToString("yyyy-MM-dd-HH");

        private static string GetLauncherFormattedTimeLong() => DateTime.UtcNow.ToString("yyyy-MM-dd-HH-mm");

        private static string GenerateUserAgent()
        {
            return string.Format(USER_AGENT_TEMPLATE, MakeComputerId());
        }
    }
}