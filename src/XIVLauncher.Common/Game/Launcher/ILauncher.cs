using System.IO;
using System.Threading.Tasks;
using XIVLauncher.Common.Game.Patch.PatchList;
using XIVLauncher.Common.PlatformAbstractions;

namespace XIVLauncher.Common.Game.Launcher;

public interface ILauncher
{
    public Task<PatchListEntry[]> CheckBootVersion(DirectoryInfo gamePath, bool forceBaseVersion = false);

    public Task<PatchListEntry[]> CheckGameVersion(DirectoryInfo gamePath, bool forceBaseVersion = false);

    // TODO(Ava): KR/CN probably don't need isFreeTrial, figure out how to abstract this better
    public Task<LoginResult> Login(string userName, string password, string otp, bool useCache, DirectoryInfo gamePath, bool forceBaseVersion, bool isFreeTrial);

    public object? LaunchGame(IGameRunner runner, string sessionId, int region, int expansionLevel,
                              string additionalArguments, DirectoryInfo gamePath, bool isDx11,
                              ClientLanguage language, bool encryptArguments, DpiAwareness dpiAwareness);

    public Task<GateStatus> GetGateStatus(ClientLanguage language);

    public Task<bool> GetLoginStatus();

    public Task<string> GenPatchToken(string patchUrl, string uniqueId);

    public Task<byte[]> DownloadAsLauncher(string url, ClientLanguage language, string contentType = "");
}
