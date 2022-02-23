using System.Windows.Input;
using CheapLoc;

namespace XIVLauncher.Windows.ViewModel
{
    class CustomMessageBoxViewModel
    {
        public ICommand CopyMessageTextCommand { get; set; }

        public CustomMessageBoxViewModel()
        {
            SetupLoc();
        }

        private void SetupLoc()
        {
            OfficialLauncherLoc = Loc.Localize("StartOfficialLauncher", "Official Launcher");
            JoinDiscordLoc = Loc.Localize("JoinDiscord", "Join Discord");
            OpenIntegrityReportLoc = Loc.Localize("OpenIntegrityReport", "Open Integrity Report");
            OpenFaqLoc = Loc.Localize("OpenFaq", "Open FAQ");
            ReportErrorLoc = Loc.Localize("ReportError", "Report error");
            OkLoc = Loc.Localize("OK", "OK");
            YesWithShortcutLoc = Loc.Localize("Yes", "_Yes");
            NoWithShortcutLoc = Loc.Localize("No", "_No");
            CancelWithShortcutLoc = Loc.Localize("Cancel", "_Cancel");
            CopyWithShortcutLoc = Loc.Localize("Copy", "_Copy");
        }

        public string OfficialLauncherLoc { get; private set; }
        public string JoinDiscordLoc { get; private set; }
        public string OpenIntegrityReportLoc { get; private set; }
        public string OpenFaqLoc { get; private set; }
        public string ReportErrorLoc { get; private set; }
        public string OkLoc { get; private set; }
        public string YesWithShortcutLoc { get; private set; }
        public string NoWithShortcutLoc { get; private set; }
        public string CancelWithShortcutLoc { get; private set; }
        public string CopyWithShortcutLoc { get; private set; }
    }
}