using XIVLauncher.Common.Game.Patch.PatchList;

namespace XIVLauncher.Common.Game;

public class LoginResult
{
    public LoginState State { get; set; }
    public PatchListEntry[] PendingPatches { get; set; }
    public OauthLoginResult OauthLogin { get; set; }
    public string UniqueId { get; set; }
}
