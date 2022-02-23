using System;

namespace XIVLauncher.Common
{
    /// <summary>
    /// Various constant values.
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// The base game version string.
        /// </summary>
        public const string BASE_GAME_VERSION = "2012.01.01.0000.0000";

        /// <summary>
        /// Gets the patcher user agent.
        /// </summary>
        public static string PatcherUserAgent => GetPatcherUserAgent(Util.GetPlatform());

        /// <summary>
        /// Get a patcher compliant User-Agent string for a given platform.
        /// </summary>
        /// <param name="platform">Platform to use.</param>
        /// <returns>A platform User-Agent string.</returns>
        private static string GetPatcherUserAgent(Platform platform)
        {
            switch (platform)
            {
                case Platform.Win32:
                case Platform.Win32OnLinux:
                    return "FFXIV PATCH CLIENT";

                case Platform.Mac:
                    return "FFXIV-MAC PATCH CLIENT";

                default:
                    throw new ArgumentOutOfRangeException(nameof(platform), platform, null);
            }
        }
    }
}
