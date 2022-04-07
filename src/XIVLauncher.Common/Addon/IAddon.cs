using System.Diagnostics;

namespace XIVLauncher.Common.Addon
{
    public interface IAddon
    {
        string Name { get; }

        void Setup(Process gameProcess);
    }
}