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
