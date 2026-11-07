using System.IO;
using XIVLauncher.Common;

namespace XIVLauncher.OtpProviders
{
    public static class OtpProviderPaths
    {
        private const string ProviderDirectoryName = "otp-providers";

        public static string GetProviderDirectory()
        {
            return Path.Combine(Paths.RoamingPath, ProviderDirectoryName);
        }

        public static string EnsureProviderDirectoryExists()
        {
            var providerDirectory = GetProviderDirectory();
            Directory.CreateDirectory(providerDirectory);
            return providerDirectory;
        }
    }
}
