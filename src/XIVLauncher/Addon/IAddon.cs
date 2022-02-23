using System.Diagnostics;
using XIVLauncher.Common;

namespace XIVLauncher.Addon
{
    public interface IAddon
    {
        string Name { get; }

        void Setup(Process gameProcess, ILauncherSettingsV3 setting);
    }
}