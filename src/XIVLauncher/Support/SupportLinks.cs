using System.Windows;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Support
{
    public static class SupportLinks
    {
        public static void OpenDiscord(object sender, RoutedEventArgs e)
        {
            PlatformHelpers.OpenBrowser("https://discord.gg/3NMcUV5");
        }

        public static void OpenFaq(object sender, RoutedEventArgs e)
        {
            PlatformHelpers.OpenBrowser("https://goatcorp.github.io/faq/");
        }
    }
}
