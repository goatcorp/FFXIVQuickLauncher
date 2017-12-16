using System;
using System.Windows.Forms;
using FolderSelect;

namespace XIVLauncher
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();

            if (Properties.Settings.Default.savedid != "")
            {
                IDTextBox.Text = Properties.Settings.Default.savedid;
                PWTextBox.Text = Properties.Settings.Default.savedpw;
                saveCheckBox.Checked = true;
            }

            if(Properties.Settings.Default.setupcomplete != true)
            {
                InitialSetup();
            }

            if(Properties.Settings.Default.autologin == true && !Settings.IsAdministrator())
            {
                try
                {
                    this.Enabled = false;

                    if (!XIVGame.GetGateStatus())
                    {
                        this.Enabled = true;
                        MessageBox.Show(
                            "Square Enix seems to be running maintenance work right now. The game shouldn't be launched.");

                        Properties.Settings.Default["autologin"] = false;
                        Properties.Settings.Default.Save();
                    }
                    else
                    {
                        XIVGame.LaunchGame(XIVGame.GetRealSid(IDTextBox.Text, PWTextBox.Text, OTPTextBox.Text), Settings.GetLanguage(), Settings.IsDX11(), Settings.GetExpansionLevel());
                        Environment.Exit(0);
                    }
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

        private void Login(object sender, EventArgs e)
        {
            if (!XIVGame.GetGateStatus())
            {
                MessageBox.Show(
                    "Square Enix seems to be running maintenance work right now. The game shouldn't be launched.");

                return;
            }

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
                XIVGame.LaunchGame(XIVGame.GetRealSid(IDTextBox.Text, PWTextBox.Text, OTPTextBox.Text), Settings.GetLanguage(), Settings.IsDX11(), Settings.GetExpansionLevel());
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

        public void InitialSetup()
        {
            MessageBox.Show(@"You will now be asked to select the path your game is installed in.
It should contain the folders ""game"" and ""boot"".", "Select Game Path", MessageBoxButtons.OK);

            FolderSelectDialog fsd = new FolderSelectDialog();
            fsd.Title = "Choose your game path";

            if (fsd.ShowDialog(IntPtr.Zero))
            {
                Properties.Settings.Default["gamepath"] = fsd.FileName;
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

        private void QueueButton_Click(object sender, EventArgs e) //TODO: please do this in a thread when you care enough at some point
        {
            DialogResult result = MessageBox.Show("This will be querying the maintenance status server, until the maintenance is over and then launch the game. Make sure the login information you entered is correct." +
                                                  "\n\n!!!The application will be unresponsive!!!\n\nDo you want to continue?", "Maintenance Queue", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);

            if (result == System.Windows.Forms.DialogResult.Yes)
            {
                this.BringToFront();

                while (true)
                {
                    if (XIVGame.GetGateStatus())
                        break;
                    System.Threading.Thread.Sleep(5000);
                }

                Console.Beep(529, 130);
                System.Threading.Thread.Sleep(200);
                Console.Beep(529, 100);
                System.Threading.Thread.Sleep(30);
                Console.Beep(529, 100);
                System.Threading.Thread.Sleep(300);
                Console.Beep(420, 140);
                System.Threading.Thread.Sleep(300);
                Console.Beep(466, 100);
                System.Threading.Thread.Sleep(300);
                Console.Beep(529, 160);
                System.Threading.Thread.Sleep(200);
                Console.Beep(466, 100);
                System.Threading.Thread.Sleep(30);
                Console.Beep(529, 900);

                Login(null, null);
            }

        }
    }
}
