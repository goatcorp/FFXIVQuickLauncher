namespace XIVLauncher.Addon
{
    public enum AddonStartAt
    {
        LauncherInitialised,
        GameLaunched,
    }

    public class AddonEntry
    {
        public bool IsEnabled { get; set; }
        public AddonStartAt StartAt { get; set; } = AddonStartAt.GameLaunched;
        public IAddon Addon { get; set; }
    }
}