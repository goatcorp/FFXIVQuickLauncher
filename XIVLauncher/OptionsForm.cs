using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace XIVLauncher
{
    public partial class OptionsForm : Form
    {
        public OptionsForm()
        {
            InitializeComponent();
            LanguageSelector.SelectedIndex = System.Convert.ToInt32(Properties.Settings.Default.language);
            dxCheckBox.Checked = Properties.Settings.Default.isdx11;
            comboBox1.SelectedIndex = Properties.Settings.Default.expansionlevel;
            pathLabel.Text = "Current Game Path:\n" + Properties.Settings.Default.gamepath;
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default["language"] = LanguageSelector.SelectedIndex;
            Properties.Settings.Default["expansionlevel"] = comboBox1.SelectedIndex;
            if (dxCheckBox.Checked) { Properties.Settings.Default["isdx11"] = true; } else { Properties.Settings.Default["isdx11"] = false; }
            Properties.Settings.Default.Save();
            this.Close();
        }

        private void LaunchBackupTool(object sender, EventArgs e)
        {
            try
            {
                Process backuptool = new Process();
                backuptool.StartInfo.FileName = Settings.GetGamePath() + "/boot/ffxivconfig.exe";
                backuptool.Start();
            }
            catch(Exception exc)
            {
                MessageBox.Show("Could not launch ffxivconfig. Is your game path correct?\n\n" + exc, "Launch failed", MessageBoxButtons.OK);
            }
        }

        private void ChangeGamePath(object sender, EventArgs e)
        {
            MessageBox.Show(@"You will now be asked to select the path your game is installed in.
It should contain the folders ""game"" and ""boot"".", "Select Game Path", MessageBoxButtons.OK);

            if (GamePathDialog.ShowDialog() == DialogResult.OK)
            {
                Properties.Settings.Default["gamepath"] = GamePathDialog.SelectedPath;
                Properties.Settings.Default.Save();
                pathLabel.Text = "Current Game Path:\n" + Properties.Settings.Default.gamepath;
            }
        }
    }
}
