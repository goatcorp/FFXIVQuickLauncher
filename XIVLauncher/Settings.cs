using System.Security.Principal;

namespace XIVLauncher
{
    static class Settings
    {

        public static string GetGamePath()
        {
            return Properties.Settings.Default.gamepath;
        }

        public static int GetLanguage()
        {
            return System.Convert.ToInt32(Properties.Settings.Default.language);
        }

        public static bool IsDX11()
        {
            return System.Convert.ToBoolean(Properties.Settings.Default.isdx11);
        }

        public static int GetExpansionLevel()
        {
            return Properties.Settings.Default.expansionlevel;
        }

        public static bool IsAdministrator()
        {
            return (new WindowsPrincipal(WindowsIdentity.GetCurrent()))
                    .IsInRole(WindowsBuiltInRole.Administrator);
        }


    }
}
