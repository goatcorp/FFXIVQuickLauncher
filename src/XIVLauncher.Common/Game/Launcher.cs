

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

namespace XIVLauncher.Common.Game;

public class Launcher
{
    private readonly ISteam? steam;
    private readonly byte[]? steamTicket;
    private readonly IUniqueIdCache uniqueIdCache;
    private readonly ISettings settings;
    private readonly HttpClient client;

    public Launcher(ISteam? steam, IUniqueIdCache uniqueIdCache, ISettings settings)
    {
        this.steam = steam;
        this.uniqueIdCache = uniqueIdCache;
        this.settings = settings;

        ServicePointManager.Expect100Continue = false;

#if NET6_0_OR_GREATER && !WIN32
        var sslOptions = new SslClientAuthenticationOptions()
        {
            CipherSuitesPolicy = new CipherSuitesPolicy(new[] { TlsCipherSuite.TLS_DHE_RSA_WITH_AES_256_GCM_SHA384 })
        };

        var handler = new SocketsHttpHandler
        {
            UseCookies = false,
            SslOptions = sslOptions,
        };
#else
        var handler = new HttpClientHandler
        {
            UseCookies = false,
        };
#endif

        this.client = new HttpClient(handler);
    }

    public Launcher(byte[] steamTicket, IUniqueIdCache uniqueIdCache, ISettings settings) : this(steam: null, uniqueIdCache, settings)
    {
        this.steamTicket = steamTicket;
    }

    // The user agent for frontier pages. {0} has to be replaced by a unique computer id and its checksum
    private const string USER_AGENT_TEMPLATE = "SQEXAuthor/2.0.0(Windows 6.2; ja-jp; {0})";
    private readonly string _userAgent = GenerateUserAgent();

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

    public async Task<LoginResult> Login(string userName, string password, string otp, bool isSteam, bool useCache, DirectoryInfo gamePath, bool forceBaseVersion, bool isFreeTrial)
    {
        string uid;
        PatchListEntry[] pendingPatches = null;

        OauthLoginResult oauthLoginResult;

        LoginState loginState;

        Log.Information("XivGame::Login(steamServiceAccount:{IsSteam}, cache:{UseCache})", isSteam, useCache);

        Ticket? steamTicket = null;

        if (isSteam)
        {
            if (this.steamTicket != null)
            {
                steamTicket = Ticket.EncryptAuthSessionTicket(this.steamTicket, (uint) DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                Log.Information("Using predefined steam ticket");
            }
            else
            {
                Debug.Assert(this.steam != null);

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

                try
                {
                    steamTicket = await Ticket.Get(steam).ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    throw new SteamException("Could not request auth ticket.", ex);
                }
            }

            if (steamTicket == null)
            {
                throw new SteamException("Steam auth ticket was null.");
            }
        }

        if (!useCache || !this.uniqueIdCache.TryGet(userName, out var cached))
        {
            oauthLoginResult = await OauthLogin(userName, password, otp, isFreeTrial, isSteam, 3, steamTicket);

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

    public Process? LaunchGame(IGameRunner runner, string sessionId, int region, int expansionLevel,
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
    /// Check ver & bck files for sanity.
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
            Log.Error("Sanity check failed for {repo}/{isBck}: {NullOrWhitespace}, {ContainsNewline}, {AllNullBytes}", repo, isBck, nullOrWhitespace, containsNewline, allNullBytes);
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

    public async Task<PatchListEntry[]> CheckBootVersion(DirectoryInfo gamePath)
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"http://patch-bootver.ffxiv.com/http/win32/ffxivneo_release_boot/{Repository.Boot.GetVer(gamePath)}/?time=" +
            GetLauncherFormattedTimeLongRounded());

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

        EnsureVersionSanity(gamePath, loginResult.MaxExpansion);
        request.Content = new StringContent(GetVersionReport(gamePath, loginResult.MaxExpansion, forceBaseVersion));

        var resp = await this.client.SendAsync(request);
        var text = await resp.Content.ReadAsStringAsync();

        // Conflict indicates that boot needs to update, we do not get a patch list or a unique ID to download patches with in this case
        if (resp.StatusCode == HttpStatusCode.Conflict)
            return (null, LoginState.NeedsPatchBoot, null);

        if (!resp.Headers.TryGetValues("X-Patch-Unique-Id", out var uidVals))
            throw new InvalidResponseException("Could not get X-Patch-Unique-Id.", text);

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
            Log.Error(text);
            throw new InvalidResponseException("Could not get STORED.", text);
        }

        string? steamUsername = null;

        if (isSteam)
        {
            var steamRegex = new Regex(@"<input name=""sqexid"" type=""hidden"" value=""(?<sqexid>.*)""\/>");
            var steamMatches = steamRegex.Matches(text);

            if (steamMatches.Count == 0)
            {
                Log.Error(text);
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

    private static string GetOauthTopUrl(int region, bool isFreeTrial, bool isSteam, Ticket steamTicket)
    {
        var url =
            $"https://ffxiv-login.square-enix.com/oauth/ffxivarr/login/top?lng=en&rgn={region}&isft={(isFreeTrial ? "1" : "0")}&cssmode=1&isnew=1&launchver=3";

        if (isSteam)
        {
            url += "&issteam=1";

            url += $"&session_ticket={steamTicket.Text}";
            url += $"&ticket_size={steamTicket.Length}";
        }

        return url;
    }

    private async Task<OauthLoginResult> OauthLogin(string userName, string password, string otp, bool isFreeTrial, bool isSteam, int region, Ticket? steamTicket)
    {
        if (isSteam && steamTicket == null)
            throw new ArgumentNullException(nameof(steamTicket), "isSteam, but steamTicket == null");

        var topUrl = GetOauthTopUrl(region, isFreeTrial, isSteam, steamTicket);
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

        if (isSteam)
        {
            if (!String.Equals(userName, topResult.SteamLinkedId, StringComparison.OrdinalIgnoreCase))
                throw new SteamWrongAccountException(userName, topResult.SteamLinkedId);

            userName = topResult.SteamLinkedId;
        }

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

    public async Task<bool> GetLoginStatus()
    {
        try
        {
            var reply = Encoding.UTF8.GetString(
                await DownloadAsLauncher(
                    $"https://frontier.ffxiv.com/worldStatus/login_status.json?_={ApiHelpers.GetUnixMillis()}", ClientLanguage.English).ConfigureAwait(true));

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
        var langCode = language.GetLangCode().Replace("-", "_");
        var formattedTime = GetLauncherFormattedTimeLong();

        return $"https://launcher.finalfantasyxiv.com/v610/index.html?rc_lang={langCode}&time={formattedTime}";
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
        return string.Format(USER_AGENT_TEMPLATE, MakeComputerId());
    }
}

#nullable restore