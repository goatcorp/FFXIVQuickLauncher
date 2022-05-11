using System;
using System.IO;
using System.Net;
using System.Net.Http;

#if NET6_0_OR_GREATER && !WIN32
using System.Net.Security;
#endif

using System.Threading.Tasks;
using Serilog;
using XIVLauncher.Common.Game.Patch.PatchList;
using XIVLauncher.Common.PlatformAbstractions;

#nullable enable

namespace XIVLauncher.Common.Game.Launcher;

public class ShandaLauncher : ILauncher
{
    private readonly ISettings settings;
    private readonly HttpClient client;

    public ShandaLauncher(ISettings settings)
    {
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

    // TODO(Ava): not 100% sure this is correct, don't quote me on it
    private const string PATCHER_USER_AGENT = "FFXIV_Patch";
    private const int CURRENT_EXPANSION_LEVEL = 4;

    public async Task<LoginResult> Login(string userName, string password, string otp, bool isSteam, bool useCache, DirectoryInfo gamePath, bool forceBaseVersion, bool isFreeTrial)
    {
        throw new NotImplementedException();
    }

    public object? LaunchGame(IGameRunner runner, string sessionId, int region, int expansionLevel,
                              bool isSteamServiceAccount, string additionalArguments,
                              DirectoryInfo gamePath, bool isDx11, ClientLanguage language,
                              bool encryptArguments, DpiAwareness dpiAwareness)
    {
        throw new NotImplementedException();
    }

    public async Task<PatchListEntry[]> CheckBootVersion(DirectoryInfo gamePath, bool forceBaseVersion = false)
    {
        return Array.Empty<PatchListEntry>();
    }

    public async Task<PatchListEntry[]> CheckGameVersion(DirectoryInfo gamePath, bool forceBaseVersion = false)
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"http://ffxivpatch01.ff14.sdo.com/http/win32/shanda_release_chs_game/{(forceBaseVersion ? Constants.BASE_GAME_VERSION : Repository.Ffxiv.GetVer(gamePath))}/");

        request.Headers.AddWithoutValidation("X-Hash-Check", "enabled");
        request.Headers.AddWithoutValidation("User-Agent", PATCHER_USER_AGENT);

        Util.EnsureVersionSanity(gamePath, CURRENT_EXPANSION_LEVEL);

        var resp = await this.client.SendAsync(request);
        var text = await resp.Content.ReadAsStringAsync();

        if (string.IsNullOrEmpty(text))
            return Array.Empty<PatchListEntry>();

        Log.Verbose("Game Patching is needed... List:\n{PatchList}", text);

        return PatchListParser.Parse(text);
    }

    public async Task<string> GenPatchToken(string patchUrl, string uniqueId)
    {
        // KR/CN don't require authentication for patches
        return patchUrl;
    }

    public async Task<GateStatus> GetGateStatus(ClientLanguage language)
    {
        throw new NotImplementedException();
    }

    public async Task<bool> GetLoginStatus()
    {
        throw new NotImplementedException();
    }

    public async Task<byte[]> DownloadAsLauncher(string url, ClientLanguage language, string contentType = "")
    {
        throw new NotImplementedException();
    }
}

#nullable restore
