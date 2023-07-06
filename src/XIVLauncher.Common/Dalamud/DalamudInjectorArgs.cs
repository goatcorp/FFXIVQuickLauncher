namespace XIVLauncher.Common.Dalamud
{
    public static class DalamudInjectorArgs
    {
        public const string LAUNCH = "launch";
        public const string WITHOUT_DALAMUD = "--without-dalamud";
        public const string FAKE_ARGUMENTS = "--fake-arguments";
        public const string NO_PLUGIN = "--no-plugin";
        public const string NO_THIRD_PARTY = "--no-3rd-plugin";
        public static string Mode(string method) => $"--mode={method}";
        public static string Game(string path) => $"--game=\"{path}\"";
        public static string HandleOwner(long handle) => $"--handle-owner={handle}";
        public static string LoggingPath(string path) => $"--logpath=\"{path}\"";
        public static string WorkingDirectory(string path) => $"--dalamud-working-directory=\"{path}\"";
        public static string ConfigurationPath(string path) => $"--dalamud-configuration-path=\"{path}\"";
        public static string PluginDirectory(string path) => $"--dalamud-plugin-directory=\"{path}\"";
        public static string AssetDirectory(string path) => $"--dalamud-asset-directory=\"{path}\"";
        public static string ClientLanguage(int language) => $"--dalamud-client-language={language}";
        public static string DelayInitialize(int delay) => $"--dalamud-delay-initialize={delay}";
        public static string TsPackB64(string data) => $"--dalamud-tspack-b64={data}";
    }
}