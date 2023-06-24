using System;

namespace XIVLauncher.Common.Dalamud
{
    [Serializable]
    public sealed class DalamudStartInfo
    {
        public string WorkingDirectory;
        public string ConfigurationPath;
        public string LoggingPath;

        public string PluginDirectory;
        public string AssetDirectory;
        public ClientLanguage Language;
        public int DelayInitializeMs;

        public string GameVersion;
        public string TroubleshootingPackData;
    }
}