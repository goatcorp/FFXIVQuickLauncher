using System;
using XIVLauncher.Game;

namespace Dalamud
{
    [Serializable]
    public sealed class DalamudStartInfo
    {
        public string WorkingDirectory;
        public string ConfigurationPath;

        public string PluginDirectory;
        public string DefaultPluginDirectory;
        public ClientLanguage Language;

        public string GameVersion;
    }
}
