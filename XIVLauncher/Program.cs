using System;
using System.Windows.Forms;

namespace XIVLauncher
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (Properties.Settings.Default.setupcomplete == true && Properties.Settings.Default.autologin == true && !Settings.IsAdministrator() && Properties.Settings.Default.otprequired == true)
            {
                OTPForm form = new OTPForm();
                Application.Run(form);
                if (form.Success)
                {
                    return;
                } else
                {
                    Properties.Settings.Default.autologin = false;
                }
            }
            Application.Run(new MainForm());
        }
    }
}
