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
        }

        public string PatchPreparingLoc { get; private set; }
        public string PatchGeneralStatusLoc { get; private set; }
        public string PatchCheckingLoc { get; private set; }
        public string PatchDoneLoc { get; private set; }
        public string PatchInstallingLoc { get; private set; }
        public string PatchInstallingFormattedLoc { get; private set; }
        public string PatchInstallingIdleLoc { get; private set; }
        public string PatchEtaLoc { get; private set; }
    }
}
