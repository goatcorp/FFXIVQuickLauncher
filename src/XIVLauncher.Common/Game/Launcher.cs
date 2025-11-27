#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;

#if NET6_0_OR_GREATER && !WIN32
using System.Net.Security;
#endif

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Serilog;
using XIVLauncher.Common.Game.Patch.PatchList;
using XIVLauncher.Common.Encryption;
using XIVLauncher.Common.Game.Exceptions;
using XIVLauncher.Common.PlatformAbstractions;
using XIVLauncher.Common.Util;
using System.Security.Authentication;

namespace XIVLauncher.Common.Game;

public class Launcher
{
    private readonly ISteam? steam;
    private readonly byte[]? overriddenSteamTicket;
    private readonly IUniqueIdCache uniqueIdCache;
    private readonly ISettings settings;
    private readonly HttpClient client;
    private readonly string frontierUrlTemplate;

    public Launcher(ISteam? steam, IUniqueIdCache uniqueIdCache, ISettings settings, string frontierUrl)
    {
        this.steam = steam;
        this.uniqueIdCache = uniqueIdCache;
        this.settings = settings;

        this.frontierUrlTemplate = frontierUrl ?? throw new Exception("Frontier URL template is null, this is now required");

        ServicePointManager.Expect100Continue = false;

        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.None,
            UseCookies = false,
            PreAuthenticate = false,
            UseProxy = false,
            UseDefaultCredentials = false,
            SslProtocols = SslProtocols.None
        };

        this.client = new HttpClient(handler, true);
    }

    public Launcher(byte[] overriddenSteamTicket, IUniqueIdCache uniqueIdCache, ISettings settings, string frontierUrl)
        : this(steam: null, uniqueIdCache, settings, frontierUrl)
    {
        this.overriddenSteamTicket = overriddenSteamTicket;
    }

    // The user agent for frontier pages. {0} has to be replaced by a unique computer id and its checksum
    private const string UserAgentTemplate = "SQEXAuthor/2.0.0(Windows 6.2; ja-jp; {0})";
    private readonly string userAgent = GenerateUserAgent();

    private static readonly string[] FilesToHash =
    {
        "ffxivboot.exe",
        "ffxivboot64.exe",
        "ffxivlauncher64.exe",
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
        public PatchListEntry[] PendingPatches { get; set; } = [];
        public OauthLoginResult? OauthLogin { get; set; }
        public string? UniqueId { get; set; }
    }

    public async Task<LoginResult> Login(string userName, string password, string otp, string recaptchaToken, bool isSteam, bool useCache, DirectoryInfo gamePath, bool forceBaseVersion, bool isFreeTrial)
    {
        string? uid;
        var pendingPatches = Array.Empty<PatchListEntry>();

        OauthLoginResult oauthLoginResult;

        LoginState loginState;

        Log.Information("XivGame::Login(steamServiceAccount:{IsSteam}, cache:{UseCache})", isSteam, useCache);

        Ticket? steamTicket = null;

        if (isSteam)
        {
            if (this.overriddenSteamTicket != null)
            {
                steamTicket = Ticket.EncryptAuthSessionTicket(this.overriddenSteamTicket, (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                Log.Information("Using predefined steam ticket");
            }
            else
            {
                if (this.steam == null)
                    throw new InvalidOperationException("No Steam instance provided, but tried to log in with Steam");

                try
                {
                    if (!this.steam.IsValid)
                    {
                        this.steam.Initialize(isFreeTrial ? Constants.STEAM_FT_APP_ID : Constants.STEAM_APP_ID);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Could not initialize Steam");
                    throw new SteamException("SteamAPI_Init() failed.", ex);
                }

                if (!this.steam.IsValid)
                {
                    throw new SteamException("Steam did not initialize successfully. Please restart Steam and try again.");
                }

                if (!this.steam.BLoggedOn)
                {
                    throw new SteamException("Not logged into Steam, or Steam is running in offline mode. Please log in and try again.");
                }

                const int NUM_TRIES = 5;

                for (var i = 0; i < NUM_TRIES; i++)
                {
                    try
                    {
                        steamTicket = await Ticket.Get(steam).ConfigureAwait(true);

                        if (steamTicket != null)
                            break;
                    }
                    catch (Exception ex)
                    {
                        throw new SteamException($"Could not request auth ticket (try {i + 1}/{NUM_TRIES})", ex);
                    }
                }
            }

            if (steamTicket == null)
            {
                throw new SteamTicketNullException();
            }
        }

        if (!useCache || !this.uniqueIdCache.TryGet(userName, out var cached))
        {
            oauthLoginResult = await OauthLogin(userName, password, otp, recaptchaToken, isFreeTrial, isSteam, 3, steamTicket);

            Log.Information("OAuth login successful - playable:{IsPlayable} terms:{TermsAccepted} region:{Region} ex:{MaxExpansion}",
                            oauthLoginResult.Playable,
                            oauthLoginResult.TermsAccepted,
                            oauthLoginResult.Region,
                            oauthLoginResult.MaxExpansion);

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

        if (loginState == LoginState.Ok && string.IsNullOrEmpty(uid))
            throw new Exception("LoginState is Ok, but UID is null or empty.");

        return new LoginResult
        {
            PendingPatches = pendingPatches,
            OauthLogin = oauthLoginResult,
            State = loginState,
            UniqueId = uid,
        };
    }

    // FOR FFXIV TC
    public Process? LaunchGame(
        IGameRunner runner, string sessionId, int region, int expansionLevel,
        bool isSteamServiceAccount, string additionalArguments,
        DirectoryInfo gamePath, ClientLanguage language,
        bool encryptArguments, DpiAwareness dpiAwareness)
    {
        Log.Information("XivGame::LaunchGame(steamServiceAccount:{IsSteam}, args:{AdditionalArguments})",
                        isSteamServiceAccount,
                        additionalArguments);
        var tcSessionId = ExchangeSessionId(sessionId).Result;
        var exePath = Path.Combine(gamePath.FullName, "game", "ffxiv_dx11.exe");
        var environment = new Dictionary<string, string>();
        var argumentBuilder = new ArgumentBuilder()
                              .Append("DEV.LobbyHost01", "neolobby01.ffxiv.com.tw")
                              .Append("DEV.LobbyPort01", "54994")
                              .Append("DEV.GMServerHost", "frontier.ffxiv.com.tw")
                              .Append("DEV.TestSID", tcSessionId)
                              .Append("SYS.resetConfig", "0")
                              .Append("DEV.SaveDataBankHost", "config-dl.ffxiv.com.tw")
                              .Append("resetConfig", "0")
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
            var regex = new Regex(@"\s*(?<key>[^\s=]+)\s*=\s*(?<value>([^=]*$|[^=]*\s(?=[^\s=]+)))\s*", RegexOptions.Compiled);
            foreach (Match match in regex.Matches(additionalArguments))
                argumentBuilder.Append(match.Groups["key"].Value, match.Groups["value"].Value.Trim());
        }

        if (!File.Exists(exePath))
            throw new BinaryNotPresentException(exePath);

        var workingDir = Path.Combine(gamePath.FullName, "game");

        var arguments = encryptArguments
                            ? argumentBuilder.BuildEncrypted()
                            : argumentBuilder.Build();

        return runner.Start(exePath, workingDir, arguments, environment, dpiAwareness);
    }

    public async Task<string> ExchangeSessionId(string token)
    {
        var requestObj = new Dictionary<string, string>{
            { "token", token }
        };
        var url = "https://user.ffxiv.com.tw/api/login/launcherSession";
        var content = new StringContent(JsonConvert.SerializeObject(requestObj), Encoding.UTF8, "application/json");
        var response = await this.client.PostAsync(url, content);
        response.EnsureSuccessStatusCode();
        var responseBody = await response.Content.ReadAsStringAsync();
        var responseObj = JsonConvert.DeserializeObject<Dictionary<string, string>>(responseBody)
            ?? throw new Exception("Failed to exchange session ID: could not parse server response.");
        if (responseObj.TryGetValue("error", out var errorNews))
        {
            throw new Exception($"Failed to exchange session ID: server returned error: {errorNews}");
        }
        if (responseObj.TryGetValue("sessionId", out var sessionId))
        {
            return sessionId;
        }
        throw new Exception("Failed to exchange session ID: server response did not contain a session ID.");
    }

    // public Process? LaunchGame(
    //     IGameRunner runner, string sessionId, int region, int expansionLevel,
    //     bool isSteamServiceAccount, string additionalArguments,
    //     DirectoryInfo gamePath, ClientLanguage language,
    //     bool encryptArguments, DpiAwareness dpiAwareness)
    // {
    //     Log.Information("XivGame::LaunchGame(steamServiceAccount:{IsSteam}, args:{AdditionalArguments})",
    //                     isSteamServiceAccount,
    //                     additionalArguments);

    //     var exePath = Path.Combine(gamePath.FullName, "game", "ffxiv_dx11.exe");
    //     var environment = new Dictionary<string, string>();

    //     var argumentBuilder = new ArgumentBuilder()
    //                           .Append("DEV.DataPathType", "1")
    //                           .Append("DEV.MaxEntitledExpansionID", expansionLevel.ToString())
    //                           .Append("DEV.TestSID", sessionId)
    //                           .Append("DEV.UseSqPack", "1")
    //                           .Append("SYS.Region", region.ToString())
    //                           .Append("language", ((int)language).ToString())
    //                           .Append("resetConfig", "0")
    //                           .Append("ver", Repository.Ffxiv.GetVer(gamePath));

    //     if (isSteamServiceAccount)
    //     {
    //         // These environment variable and arguments seems to be set when ffxivboot is started with "-issteam" (27.08.2019)
    //         environment.Add("IS_FFXIV_LAUNCH_FROM_STEAM", "1");
    //         argumentBuilder.Append("IsSteam", "1");
    //     }

    //     // This is a bit of a hack; ideally additionalArguments would be a dictionary or some KeyValue structure
    //     if (!string.IsNullOrEmpty(additionalArguments))
    //     {
    //         var regex = new Regex(@"\s*(?<key>[^\s=]+)\s*=\s*(?<value>([^=]*$|[^=]*\s(?=[^\s=]+)))\s*", RegexOptions.Compiled);
    //         foreach (Match match in regex.Matches(additionalArguments))
    //             argumentBuilder.Append(match.Groups["key"].Value, match.Groups["value"].Value.Trim());
    //     }

    //     if (!File.Exists(exePath))
    //         throw new BinaryNotPresentException(exePath);

    //     var workingDir = Path.Combine(gamePath.FullName, "game");

    //     var arguments = encryptArguments
    //                         ? argumentBuilder.BuildEncrypted()
    //                         : argumentBuilder.Build();

    //     return runner.Start(exePath, workingDir, arguments, environment, dpiAwareness);
    // }

    private static string GetVersionReport(DirectoryInfo gamePath, int exLevel, bool forceBaseVersion)
    {
        var verReport =  "\n";  //$"{GetBootVersionHash(gamePath)}\n";  // TC Region skip boot version check

        if (exLevel >= 1)
            verReport += $"ex1\t{(forceBaseVersion ? Constants.BASE_GAME_VERSION : Repository.Ex1.GetVer(gamePath))}\n";

        if (exLevel >= 2)
            verReport += $"ex2\t{(forceBaseVersion ? Constants.BASE_GAME_VERSION : Repository.Ex2.GetVer(gamePath))}\n";

        if (exLevel >= 3)
            verReport += $"ex3\t{(forceBaseVersion ? Constants.BASE_GAME_VERSION : Repository.Ex3.GetVer(gamePath))}\n";

        if (exLevel >= 4)
            verReport += $"ex4\t{(forceBaseVersion ? Constants.BASE_GAME_VERSION : Repository.Ex4.GetVer(gamePath))}\n";

        if (exLevel >= 5)
            verReport += $"ex5\t{(forceBaseVersion ? Constants.BASE_GAME_VERSION : Repository.Ex5.GetVer(gamePath))}\n";

        return verReport;
    }

    /// <summary>
    /// Check ver and bck files for sanity.
    /// </summary>
    /// <param name="gamePath"></param>
    /// <param name="exLevel"></param>
    private static void EnsureVersionSanity(DirectoryInfo gamePath, int exLevel)
    {
        var failed = IsBadVersionSanity(gamePath, Repository.Ffxiv);
        failed |= IsBadVersionSanity(gamePath, Repository.Ffxiv, true);

        if (exLevel >= 1)
        {
            failed |= IsBadVersionSanity(gamePath, Repository.Ex1);
            failed |= IsBadVersionSanity(gamePath, Repository.Ex1, true);
        }

        if (exLevel >= 2)
        {
            failed |= IsBadVersionSanity(gamePath, Repository.Ex2);
            failed |= IsBadVersionSanity(gamePath, Repository.Ex2, true);
        }

        if (exLevel >= 3)
        {
            failed |= IsBadVersionSanity(gamePath, Repository.Ex3);
            failed |= IsBadVersionSanity(gamePath, Repository.Ex3, true);
        }

        if (exLevel >= 4)
        {
            failed |= IsBadVersionSanity(gamePath, Repository.Ex4);
            failed |= IsBadVersionSanity(gamePath, Repository.Ex4, true);
        }

        if (exLevel >= 5)
        {
            failed |= IsBadVersionSanity(gamePath, Repository.Ex5);
            failed |= IsBadVersionSanity(gamePath, Repository.Ex5, true);
        }

        if (failed)
            throw new InvalidVersionFilesException();
    }

    private static bool IsBadVersionSanity(DirectoryInfo gamePath, Repository repo, bool isBck = false)
    {
        var text = repo.GetVer(gamePath, isBck);

        var nullOrWhitespace = string.IsNullOrWhiteSpace(text);
        var containsNewline = text.Contains("\n");
        var allNullBytes = Encoding.UTF8.GetBytes(text).All(x => x == 0x00);

        if (nullOrWhitespace || containsNewline || allNullBytes)
        {
            Log.Error("Sanity check failed for {Repo}/{IsBck}: {NullOrWhitespace}, {ContainsNewline}, {AllNullBytes}", repo, isBck, nullOrWhitespace, containsNewline, allNullBytes);
            return true;
        }

        return false;
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

    public async Task<PatchListEntry[]> CheckBootVersion(DirectoryInfo gamePath, bool forceBaseVersion = false)
    {
        var bootVersion = forceBaseVersion ? Constants.BASE_GAME_VERSION : Repository.Boot.GetVer(gamePath);
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"http://patch-bootver.ffxiv.com/http/win32/ffxivneo_release_boot/{bootVersion}/?time=" +
            GetLauncherFormattedTimeLongRounded());

        request.Headers.AddWithoutValidation("User-Agent", Constants.PatcherUserAgent);
        request.Headers.AddWithoutValidation("Host", "patch-bootver.ffxiv.com");

        var resp = await this.client.SendAsync(request);
        var text = await resp.Content.ReadAsStringAsync();

        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<PatchListEntry>();

        Log.Verbose("Boot patching is needed... List:\n{PatchList}", resp);

        try
        {
            return PatchListParser.Parse(text);
        }
        catch (PatchListParseException ex)
        {
            Log.Information("Patch list:\n{PatchList}", ex.List);
            throw;
        }
    }
    // FOR FFXIV TC
    private async Task<(string? Uid, LoginState result, PatchListEntry[] PendingGamePatches)> RegisterSession(OauthLoginResult loginResult, DirectoryInfo gamePath, bool forceBaseVersion)
    {
        var request = new HttpRequestMessage(HttpMethod.Post,
                                             $"http://patch-gamever.ffxiv.com.tw/http/win32/ffxivtc_release_tc_game/{(forceBaseVersion ? Constants.BASE_GAME_VERSION : Repository.Ffxiv.GetVer(gamePath))}/{loginResult.SessionId}");

        request.Headers.AddWithoutValidation("Connection", "Keep-Alive");
        request.Headers.AddWithoutValidation("User-Agent", Constants.PatcherUserAgent);
        request.Headers.AddWithoutValidation("X-Hash-Check", "enabled");

        if (!forceBaseVersion)
            EnsureVersionSanity(gamePath, loginResult.MaxExpansion);
        request.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(GetVersionReport(gamePath, loginResult.MaxExpansion, forceBaseVersion)));

        var resp = await this.client.SendAsync(request);
        var text = await resp.Content.ReadAsStringAsync();

        /*
         * Conflict indicates that boot needs to update, we do not get a patch list or a unique ID to download patches with in this case.
         * This means that server requested us to patch Boot, even though in order to get to this place, we just checked for boot patches.
         *
         * In turn, that means that something or someone modified boot binaries without our involvement.
         * We have no way to go back to a "known" good state other than to do a full reinstall.
         *
         * This has happened multiple times with users that have viruses that infect other EXEs and change their hashes, causing the update
         * server to reject our boot hashes.
         *
         * In the future we may be able to just delete /boot and run boot patches again, but this doesn't happen often enough to warrant the
         * complexity and if boot is broken, the game probably is too.
         */
        if (resp.StatusCode == HttpStatusCode.Conflict)
            return (null, LoginState.NeedsPatchBoot, Array.Empty<PatchListEntry>());

        if (resp.StatusCode == HttpStatusCode.Gone)
            throw new InvalidResponseException("The server indicated that the version requested is no longer being serviced or not present.", text);

        if (!resp.Headers.TryGetValues("X-Patch-Unique-Id", out var uidVals))
            throw new InvalidResponseException($"Could not get X-Patch-Unique-Id. ({resp.StatusCode})", text);

        var uid = uidVals.First();

        if (string.IsNullOrEmpty(text))
            return (uid, LoginState.Ok, Array.Empty<PatchListEntry>());

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
        request.Headers.AddWithoutValidation("User-Agent", this.userAgent);
        request.Headers.AddWithoutValidation("Connection", "Keep-Alive");
        request.Headers.AddWithoutValidation("Cookie", "_rsid=\"\"");

        var reply = await this.client.SendAsync(request);

        var text = await reply.Content.ReadAsStringAsync();

        if (text.Contains("window.external.user(\"restartup\");"))
        {
            if (isSteam)
                throw new SteamLinkNeededException();

            throw new InvalidResponseException("restartup, but not isSteam?", text);
        }

        var storedRegex = new Regex(@"\t<\s*input .* name=""_STORED_"" value=""(?<stored>.*)"">");
        var matches = storedRegex.Matches(text);

        if (matches.Count == 0)
        {
            Log.Error("Could not get STORED. Page:\n{Text}", text);
            throw new InvalidResponseException("Could not get STORED.", text);
        }

        string? steamUsername = null;

        if (isSteam)
        {
            var steamRegex = new Regex(@"<input name=""sqexid"" type=""hidden"" value=""(?<sqexid>.*)""\/>");
            var steamMatches = steamRegex.Matches(text);

            if (steamMatches.Count == 0)
            {
                Log.Error("Could not get steam username. Page:\n{Text}", text);
                throw new InvalidResponseException("Could not get steam username.", text);
            }

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

    private static string GetOauthTopUrl(int region, bool isFreeTrial, bool isSteam, Ticket? steamTicket)
    {
        var url =
            $"https://ffxiv-login.square-enix.com/oauth/ffxivarr/login/top?lng=en&rgn={region}&isft={(isFreeTrial ? "1" : "0")}&cssmode=1&isnew=1&launchver=3";

        if (isSteam)
        {
            if (steamTicket == null)
                throw new ArgumentNullException(nameof(steamTicket), "isSteam, but steamTicket == null");

            if (string.IsNullOrWhiteSpace(steamTicket.Text))
                throw new ArgumentException("Steam ticket is empty", nameof(steamTicket));

            url += "&issteam=1";

            url += $"&session_ticket={steamTicket.Text}";
            url += $"&ticket_size={steamTicket.Length}";
        }

        return url;
    }
    
    // FOR FFXIV TC
    private async Task<OauthLoginResult> OauthLogin(string userName, string password, string otp, string token, bool isFreeTrial, bool isSteam, int region, Ticket? steamTicket)
    {

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://user.ffxiv.com.tw/api/login/launcherLogin");
        var loginData = new Dictionary<string, string>
        {
            { "email", ToHexString(userName).ToLowerInvariant() },
            { "password", ToHexString(password).ToLowerInvariant() },
            { "code", otp },
            { "token", token }
            
        };
        var content = new StringContent(JsonConvert.SerializeObject(loginData), Encoding.UTF8, "application/json");
        httpRequest.Content = content;
        var response = await this.client.SendAsync(httpRequest);

        var reply = await response.Content.ReadAsStringAsync();

        //TODO: 取到Error massage或是取不到token，代表登入失敗惹。
        var loginResult = JsonConvert.DeserializeObject<Dictionary<string, object>>(reply);
        if (loginResult.TryGetValue("error", out var error) || !loginResult.TryGetValue("token", out var sessionId))
        {
            throw new OauthLoginException($"Login failed: {error}");
        }
        var remainSeconds = int.Parse(loginResult["remain"].ToString() ?? "0");
        return new OauthLoginResult
        {
            SessionId = sessionId.ToString(),
            Region = 1, // Taiwan No 1!!
            TermsAccepted = true,
            Playable = remainSeconds > 0,
            MaxExpansion = 7
        };
        string ToHexString(string str)
        {
            var bytes = Encoding.UTF8.GetBytes(str);
            var stringBuilder = new StringBuilder(bytes.Length + bytes.Length);
            foreach (var num in bytes)
                stringBuilder.Append(num.ToString("x2"));
            return stringBuilder.ToString();
        }
    }

    private static string GetFileHash(string file)
    {
        var bytes = File.ReadAllBytes(file);

        var hash = SHA1.Create().ComputeHash(bytes);
        var hashstring = string.Join("", hash.Select(b => b.ToString("x2")).ToArray());

        var length = new FileInfo(file).Length;

        return length + "/" + hashstring;
    }

    public async Task<GateStatus> GetGateStatus(ClientLanguage language)
    {
        try
        {
            var reply = Encoding.UTF8.GetString(
                await DownloadAsLauncher(
                    $"https://frontier.ffxiv.com/worldStatus/gate_status.json?lang={language.GetLangCode()}&_={ApiHelpers.GetUnixMillis()}", language).ConfigureAwait(true));

            return JsonConvert.DeserializeObject<GateStatus>(reply);
        }
        catch (Exception exc)
        {
            throw new Exception("Could not get gate status", exc);
        }
    }

    public async Task<GateStatus> GetLoginStatus()
    {
        try
        {
            var reply = Encoding.UTF8.GetString(
                await DownloadAsLauncher(
                    $"https://frontier.ffxiv.com/worldStatus/login_status.json?_={ApiHelpers.GetUnixMillis()}", ClientLanguage.English).ConfigureAwait(true));

            return JsonConvert.DeserializeObject<GateStatus>(reply);
        }
        catch (Exception exc)
        {
            throw new Exception("Could not get login status", exc);
        }
    }

    private static string MakeComputerId()
    {
        var hashString = Environment.MachineName + Environment.UserName + Environment.OSVersion +
                         Environment.ProcessorCount;

        using var sha1 = SHA1.Create();

        var bytes = new byte[5];

        Array.Copy(sha1.ComputeHash(Encoding.Unicode.GetBytes(hashString)), 0, bytes, 1, 4);

        var checkSum = (byte)-(bytes[1] + bytes[2] + bytes[3] + bytes[4]);
        bytes[0] = checkSum;

        return BitConverter.ToString(bytes).Replace("-", "").ToLower();
    }

    public async Task<byte[]> DownloadAsLauncher(string url, ClientLanguage language, string contentType = "")
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);

        request.Headers.AddWithoutValidation("User-Agent", this.userAgent);

        if (!string.IsNullOrEmpty(contentType))
        {
            request.Headers.AddWithoutValidation("Accept", contentType);
        }

        request.Headers.AddWithoutValidation("Accept-Encoding", "gzip, deflate");
        request.Headers.AddWithoutValidation("Accept-Language", this.settings.AcceptLanguage);

        request.Headers.AddWithoutValidation("Origin", "https://launcher.finalfantasyxiv.com");

        request.Headers.AddWithoutValidation("Referer", GenerateFrontierReferer(language));
        request.Headers.AddWithoutValidation("Connection", "Keep-Alive");

        var resp = await this.client.SendAsync(request);
        return await resp.Content.ReadAsByteArrayAsync();
    }

    private string GenerateFrontierReferer(ClientLanguage language)
    {
        var langCode = language.GetLangCode().Replace("-", "_");
        var formattedTime = GetLauncherFormattedTimeLong();

        return string.Format(this.frontierUrlTemplate, langCode, formattedTime);
    }

    // Used to be used for frontier top, they now use the un-rounded long timestamp
    private static string GetLauncherFormattedTime() => DateTime.UtcNow.ToString("yyyy-MM-dd-HH");

    private static string GetLauncherFormattedTimeLong() => DateTime.UtcNow.ToString("yyyy-MM-dd-HH-mm");

    private static string GetLauncherFormattedTimeLongRounded()
    {
        var formatted = DateTime.UtcNow.ToString("yyyy-MM-dd-HH-mm", new CultureInfo("en-US")).ToCharArray();
        formatted[15] = '0';

        return new string(formatted);
    }

    private static string GenerateUserAgent()
    {
        return string.Format(UserAgentTemplate, MakeComputerId());
    }
}
