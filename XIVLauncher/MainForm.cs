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

            if(Properties.Settings.Default.savedid != "")
            {
                IDTextBox.Text = Properties.Settings.Default.savedid;
                PWTextBox.Text = Properties.Settings.Default.savedpw;
                saveCheckBox.Checked = true;
            }

            if(Properties.Settings.Default.setupcomplete != true)
            {
                initialSetup();
            }

            if(Properties.Settings.Default.autologin == true && !Settings.IsAdministrator())
            {
                try
                {
                    this.Enabled = false;
                    XIVGame.launchGame(XIVGame.getRealSID(IDTextBox.Text, PWTextBox.Text, OTPTextBox.Text), Settings.GetLanguage(), Settings.IsDX11(), Settings.GetExpansionLevel());
                    Environment.Exit(0);
                }
                catch(Exception e)
                {
                    this.Enabled = true;
                    MessageBox.Show("Logging in failed, check your login information or try again.\n\n" + e, "Login failed", MessageBoxButtons.OK);
                }
            }
            else
            {
                Properties.Settings.Default["autologin"] = false;
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
            if (saveCheckBox.Checked)
            {
                Properties.Settings.Default["savedid"] = IDTextBox.Text;
                Properties.Settings.Default["savedpw"] = PWTextBox.Text;
                if (autoLoginCheckBox.Checked)
                {
                    DialogResult result = MessageBox.Show("This option will log you in automatically with the credentials you entered.\nTo reset it again, launch this application as administrator once.\n\nDo you really want to enable it?", "Enabling Autologin", MessageBoxButtons.YesNo);

                    if (result == System.Windows.Forms.DialogResult.No)
                    {
                        autoLoginCheckBox.Checked = false;

                    }
                    else
                    {
                        Properties.Settings.Default["autologin"] = true;
                    }
                } else { Properties.Settings.Default["autologin"] = false; }
                Properties.Settings.Default.Save();
            }
            else
            {
                Properties.Settings.Default["savedid"] = "";
                Properties.Settings.Default["savedpw"] = "";
                Properties.Settings.Default.Save();
            }

            StatusLabel.Text = "Logging in...";
            try
            {
                XIVGame.launchGame(XIVGame.getRealSID(IDTextBox.Text, PWTextBox.Text, OTPTextBox.Text), Settings.GetLanguage(), Settings.IsDX11(), Settings.GetExpansionLevel());
                Environment.Exit(0);
            }
            catch(Exception exc)
            {
                MessageBox.Show("Logging in failed, check your login information or try again.\n\n" + exc, "Login failed", MessageBoxButtons.OK);
                StatusLabel.Text = "";
                return;
            }
        }

        private void SaveBox_CheckedChanged(object sender, EventArgs e)
        {
            if (saveCheckBox.Checked) { autoLoginCheckBox.Enabled = true; } else
            {
                autoLoginCheckBox.Enabled = false;
                autoLoginCheckBox.Checked = false;
            }
        }

        public void initialSetup()
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

            DialogResult dxresult = MessageBox.Show("Do you want to use DirectX 11?", " ", MessageBoxButtons.YesNo, MessageBoxIcon.None, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);

            if (dxresult == System.Windows.Forms.DialogResult.Yes) { Properties.Settings.Default["isdx11"] = true; } else { Properties.Settings.Default["isdx11"] = false; }

            ExpansionSelector exSelector = new ExpansionSelector();
            exSelector.ShowDialog();

            Properties.Settings.Default["setupcomplete"] = true;
            Properties.Settings.Default.Save();
        }
    }
}
