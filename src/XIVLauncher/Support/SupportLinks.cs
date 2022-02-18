using System.Diagnostics;
using System.Windows;

namespace XIVLauncher.Support
{
    public static class SupportLinks
    {
        public static void OpenDiscord(object sender, RoutedEventArgs e)
        {
            Process.Start("https://discord.gg/3NMcUV5");
        }

        public static void OpenFaq(object sender, RoutedEventArgs e)
        {
            Process.Start("https://goatcorp.github.io/faq/");
        }
    }
}