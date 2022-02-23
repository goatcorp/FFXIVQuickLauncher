namespace XIVLauncher.Common
{
    /// <summary>
    /// Various environment values.
    /// </summary>
    public static class EnvironmentSettings
    {
        /// <summary>
        /// Gets a value indicating whether XL_WINEONLINUX has been set.
        /// </summary>
        public static bool IsWine => CheckEnvBool("XL_WINEONLINUX");

        /// <summary>
        /// Gets a value indicating whether XL_NOAUTOUPDATE has been set.
        /// </summary>
        public static bool IsDisableUpdates => CheckEnvBool("XL_NOAUTOUPDATE");

        /// <summary>
        /// Gets a value indicating whether XL_PRERELEASE has been set.
        /// </summary>
        public static bool IsPreRelease => CheckEnvBool("XL_PRERELEASE");

        /// <summary>
        /// Gets a value indicating whether XL_NO_RUNAS has been set.
        /// </summary>
        public static bool IsNoRunas => CheckEnvBool("XL_NO_RUNAS");

        /// <summary>
        /// Gets the value within an environment value. It should be parseable as a boolean, or it will default to <see langword="false"/>.
        /// </summary>
        /// <param name="var">Environment variable name.</param>
        /// <returns>The parsed value within the variable.</returns>
        private static bool CheckEnvBool(string var) => bool.Parse(System.Environment.GetEnvironmentVariable(var) ?? "false");
    }
}
