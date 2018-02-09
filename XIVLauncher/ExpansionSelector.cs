using System;
using System.Windows.Forms;

namespace XIVLauncher
{
    public partial class ExpansionSelector : Form
    {
        public ExpansionSelector()
        {
            InitializeComponent();

            comboBox1.SelectedIndex = 2;
            BringToFront();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default["expansionlevel"] = comboBox1.SelectedIndex;
            Properties.Settings.Default.Save();
            Close();
        }
    }
}
