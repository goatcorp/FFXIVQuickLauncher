using System.Diagnostics;

namespace XIVLauncher.Addon
{
    public interface IAddon
    {
        void Run(Process gameProcess);
        string Name { get; }
    }
}