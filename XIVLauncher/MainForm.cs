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
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
            IDTextBox.Text = Properties.Settings.Default.savedid;
            PWTextBox.Text = Properties.Settings.Default.savedpw;

            if(Properties.Settings.Default.setupcomplete != "true")
            {
                InitialSetup();
            }

            if(Properties.Settings.Default.autologin == "true" && !Settings.IsAdministrator())
            {
                try
                {
                    this.Enabled = false;
                    XIVGame.LaunchGame(XIVGame.GetRealSID(IDTextBox.Text, PWTextBox.Text, OTPTextBox.Text), Settings.GetLanguage(), Settings.isDX11());
                    this.Close();
                }
                catch
                {
                    this.Enabled = true;
                    MessageBox.Show("Logging in failed, check your login information or try again.", "Login failed", MessageBoxButtons.OK);
                }
            }
            else
            {
                Properties.Settings.Default["autologin"] = "false";
                Properties.Settings.Default.Save();
            }
        }

        private void OpenOptions(object sender, EventArgs e)
        {
            this.Enabled = false;
            OptionsForm options = new OptionsForm();
            options.ShowDialog();
            this.Enabled = true;
        }

        private void login(object sender, EventArgs e)
        {
            if (SaveBox.Checked)
            {
                Properties.Settings.Default["savedid"] = IDTextBox.Text;
                Properties.Settings.Default["savedpw"] = PWTextBox.Text;
                if (AutoLoginBox.Checked)
                {
                    DialogResult result = MessageBox.Show("This option will log you in automatically with the credentials you entered.\nTo reset it again, launch this application as administrator once.\n\nDo you really want to enable it?", "Enabling Autologin", MessageBoxButtons.YesNo);

                    if (result == System.Windows.Forms.DialogResult.No)
                    {
                        AutoLoginBox.Checked = false;

                    }
                    else
                    {
                        Properties.Settings.Default["autologin"] = "true";
                    }
                } else { Properties.Settings.Default["autologin"] = "false"; }
                Properties.Settings.Default.Save();
            }

            

            label4.Text = "Logging in...";
            try
            {
                XIVGame.LaunchGame(XIVGame.GetRealSID(IDTextBox.Text, PWTextBox.Text, OTPTextBox.Text), Settings.GetLanguage(), Settings.isDX11());
            }
            catch(Exception exc)
            {
                MessageBox.Show("Logging in failed, check your login information or try again.", "Login failed", MessageBoxButtons.OK);
                label4.Text = "";
                return;
            }
            
            this.Close();
        }

        private void SaveBox_CheckedChanged(object sender, EventArgs e)
        {
            if (SaveBox.Checked) { AutoLoginBox.Enabled = true; } else
            {
                AutoLoginBox.Enabled = false;
                AutoLoginBox.Checked = false;
            }
        }

        public void InitialSetup()
        {
            MessageBox.Show(@"You will now be asked to select the path your game is installed in.
It should contain the folders ""game"" and ""boot"".", "Select Game Path", MessageBoxButtons.OK);

            if (GamePathDialog.ShowDialog() == DialogResult.OK)
            {
                Properties.Settings.Default["gamepath"] = GamePathDialog.SelectedPath;
            }
            else
            {
                Environment.Exit(0);
            }

            DialogResult result = MessageBox.Show("Do you want to use DirectX 11?", "", MessageBoxButtons.YesNo);

            if (result == System.Windows.Forms.DialogResult.Yes) { Properties.Settings.Default["isdx11"] = "true"; } else { Properties.Settings.Default["isdx11"] = "false"; }

            Properties.Settings.Default["setupcomplete"] = "true";
        }
    }
}
