using System.Diagnostics;

namespace XIVLauncher.Addon
{
    public interface IAddon
    {
        string Name { get; }

        void Setup(Process gameProcess, Settings setting);
    }
}