using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

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

        public static bool isDX11()
        {
            return System.Convert.ToBoolean(Properties.Settings.Default.isdx11);
        }

        public static bool IsAdministrator()
        {
            return (new WindowsPrincipal(WindowsIdentity.GetCurrent()))
                    .IsInRole(WindowsBuiltInRole.Administrator);
        }

        
    }
}
