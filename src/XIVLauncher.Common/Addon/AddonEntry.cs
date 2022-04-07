using XIVLauncher.Common.Addon.Implementations;

namespace XIVLauncher.Common.Addon
{
    public class AddonEntry
    {
        public bool IsEnabled { get; set; }
        public GenericAddon Addon { get; set; }
    }
}