using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace XIVLauncher
{
    public partial class OTPForm : Form
    {
        public bool Success { get; set; }

        public OTPForm()
        {
            InitializeComponent();
            Success = false;
        }

        private void OTPForm_Load(object sender, EventArgs e)
        {
            this.otpField.KeyPress += new System.Windows.Forms.KeyPressEventHandler(OTPFieldEnterKeyPress);
        }

        private void OTPFieldEnterKeyPress(object sender, System.Windows.Forms.KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Return)
            {
                try
                {
                    XIVGame.Login(Properties.Settings.Default.savedid, Properties.Settings.Default.savedpw, otpField.Text);
                    Success = true;
                    Close();
                }
                catch (Exception exc)
                {
                    Success = false;
                    Util.ShowError("Logging in failed, check your login information or try again.\n\n" + exc, "Login failed");
                    Properties.Settings.Default.autologin = false;
                    Close();
                }
            }
        }
    }
}
