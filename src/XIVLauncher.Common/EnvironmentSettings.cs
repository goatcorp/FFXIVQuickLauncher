namespace XIVLauncher.Common
{
    public static class EnvironmentSettings
    {
        public static bool IsWine => CheckEnvBool("XL_WINEONLINUX");
        public static bool IsHardwareRendered => CheckEnvBool("XL_HWRENDER");
        public static bool IsDisableUpdates => CheckEnvBool("XL_NOAUTOUPDATE");
        public static bool IsPreRelease => CheckEnvBool("XL_PRERELEASE");
        public static bool IsNoKillswitch => CheckEnvBool("XL_NO_KILLSWITCH");
        public static bool IsNoRunas => CheckEnvBool("XL_NO_RUNAS");
        public static bool IsIgnoreSpaceRequirements => CheckEnvBool("XL_NO_SPACE_REQUIREMENTS");
        public static bool IsOpenSteamMinimal => CheckEnvBool("XL_OPEN_STEAM_MINIMAL");
        private static bool CheckEnvBool(string var) => bool.Parse(System.Environment.GetEnvironmentVariable(var) ?? "false");
    }
}
