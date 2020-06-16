using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Serilog;
using XIVLauncher.Http;
using XIVLauncher.Windows.ViewModel;

namespace XIVLauncher.Windows
{
    /// <summary>
    /// Interaction logic for PatchDownloadDialog.xaml
    /// </summary>
    public partial class PatchDownloadDialog : Window
    {
        public PatchDownloadDialog()
        {
            InitializeComponent();
            this.DataContext = new UpdateLoadingDialogViewModel();
        }

        public void SetGeneralProgress(int curr, int final)
        {
            PatchProgressText.Text = string.Format(((UpdateLoadingDialogViewModel) DataContext).AutoLoginHintLoc,
                $"{curr}/{final}");
        }

        public void SetLeft(long left, long rate)
        {
            BytesLeftText.Text = $"{Util.BytesToString(left)} left to download at {Util.BytesToString(rate)}/s.";
        }

        public void SetPatchProgress(int index, string patchName, double pct)
        {
            switch (index)
            {
                case 0:
                    SetProgressBar1Progress(patchName, pct);
                    break;
                case 1:
                    SetProgressBar2Progress(patchName, pct);
                    break;
                case 2:
                    SetProgressBar3Progress(patchName, pct);
                    break;
                case 3:
                    SetProgressBar4Progress(patchName, pct);
                    break;
            }
        }

        public void SetProgressBar1Progress(string patchName, double percentage)
        {
            Progress1.Value = percentage;
            Progress1Text.Text = patchName;
        }

        public void SetProgressBar2Progress(string patchName, double percentage)
        {
            Progress2.Value = percentage;
            Progress2Text.Text = patchName;
        }

        public void SetProgressBar3Progress(string patchName, double percentage)
        {
            Progress3.Value = percentage;
            Progress3Text.Text = patchName;
        }

        public void SetProgressBar4Progress(string patchName, double percentage)
        {
            Progress4.Value = percentage;
            Progress4Text.Text = patchName;
        }
    }
}
