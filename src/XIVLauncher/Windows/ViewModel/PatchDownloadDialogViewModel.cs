using CheapLoc;

namespace XIVLauncher.Windows.ViewModel
{
    public class PatchDownloadDialogViewModel
    {
        public PatchDownloadDialogViewModel()
        {
            SetupLoc();
        }

        private void SetupLoc()
        {
            PatchPreparingLoc = Loc.Localize("PatchPreparing", "Preparing...");
            PatchGeneralStatusLoc = Loc.Localize("PatchGeneralStatus", "Patching through {0} updates...");
            PatchCheckingLoc = Loc.Localize("PatchChecking", "Checking...");
            PatchDoneLoc = Loc.Localize("PatchDone", "Download done!");
            PatchInstallingLoc = Loc.Localize("PatchInstalling", "Installing...");
            PatchInstallingFormattedLoc = Loc.Localize("PatchInstallingFormatted", "Installing #{0}...");
            PatchInstallingIdleLoc = Loc.Localize("PatchInstallingIdle", "Waiting for download...");
            PatchEtaLoc = Loc.Localize("PatchEta", "{0} left to download at {1}/s.");
            PatchEtaTimeLoc = new[]
            {
                Loc.Localize("PatchEtaTimeDays", "Download ETA: {0}d {1}h {2}m {3}s"),
                Loc.Localize("PatchEtaTimeHours", "Download ETA: {0}h {1}m {2}s"),
                Loc.Localize("PatchEtaTimeMinutes", "Download ETA: {0}m {1}s"),
                Loc.Localize("PatchEtaTimeSeconds", "Download ETA: {0}s"),
            };
        }

        public string PatchPreparingLoc { get; private set; }
        public string PatchGeneralStatusLoc { get; private set; }
        public string PatchCheckingLoc { get; private set; }
        public string PatchDoneLoc { get; private set; }
        public string PatchInstallingLoc { get; private set; }
        public string PatchInstallingFormattedLoc { get; private set; }
        public string PatchInstallingIdleLoc { get; private set; }
        public string PatchEtaLoc { get; private set; }
        public string[] PatchEtaTimeLoc { get; private set; }
    }
}
