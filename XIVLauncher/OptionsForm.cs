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
    public partial class OptionsForm : Form
    {
        public OptionsForm()
        {
            InitializeComponent();
            LanguageSelector.SelectedIndex = System.Convert.ToInt32(Properties.Settings.Default.language);
            checkBox1.Checked = System.Convert.ToBoolean(Properties.Settings.Default.isdx11);
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default["language"] = LanguageSelector.SelectedIndex.ToString();
            if (checkBox1.Checked) { Properties.Settings.Default["isdx11"] = "true"; } else { Properties.Settings.Default["isdx11"] = "false"; }
            Properties.Settings.Default.Save();
            this.Close();
        }
    }
}
