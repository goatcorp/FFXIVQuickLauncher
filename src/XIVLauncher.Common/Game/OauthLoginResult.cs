namespace XIVLauncher.Common.Game;

public class OauthLoginResult
{
    public string SessionId { get; set; }
    public int Region { get; set; }
    public bool TermsAccepted { get; set; }
    public bool Playable { get; set; }
    public int MaxExpansion { get; set; }
}
