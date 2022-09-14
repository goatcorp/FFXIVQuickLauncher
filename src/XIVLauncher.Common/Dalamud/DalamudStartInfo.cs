using System;

namespace XIVLauncher.Common.Dalamud
{
    [Serializable]
    public sealed class DalamudStartInfo
    {
        public string WorkingDirectory;
        public string ConfigurationPath;

        public string PluginDirectory;
        public string DefaultPluginDirectory;
        public string AssetDirectory;
        public ClientLanguage Language;
        public int DelayInitializeMs;

        public string GameVersion;
        public string TroubleshootingPackData;
    }
}