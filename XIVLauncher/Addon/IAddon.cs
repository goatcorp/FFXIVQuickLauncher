using System.Diagnostics;
using XIVLauncher.Settings;

namespace XIVLauncher.Addon
{
    public interface IAddon
    {
        string Name { get; }

        void Setup(Process gameProcess, ILauncherSettingsV3 setting);
    }
}