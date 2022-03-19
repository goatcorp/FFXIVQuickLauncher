using System;
using System.Windows.Input;
using CheapLoc;

namespace XIVLauncher.Windows.ViewModel
{
    class GameRepairProgressWindowViewModel
    {
        public GameRepairProgressWindowViewModel()
        {
            SetupLoc();
        }

        private void SetupLoc()
        {
            DownloadingMetaLoc = Loc.Localize("NowDownloadingMeta", "Downloading meta files...");
            VerifyingLoc = Loc.Localize("NowVerifying", "Verifying game files...");
            RepairingLoc = Loc.Localize("NowRepairing", "Repairing game files...");
            ConnectingLoc = Loc.Localize("NowRepairingConnecting", "Connecting...");
            ReattemptWaitingLoc = Loc.Localize("NowRepairingReattemptWaiting", "Waiting before trying again...");
            FinishingLoc = Loc.Localize("NowRepairingFinishing", "Finishing...");
            CancelWithShortcutLoc = Loc.Localize("Cancel", "_Cancel");
            SpeedUnitPerSecLoc = Loc.Localize("SpeedUnitPerSecLoc", "{0}/s");
            EstimatedRemainingDurationLoc = Loc.Localize("EstimatedRemainingDuration", "{0:00}:{1:00} remaining");
            EstimatedRemainingDurationWithHoursLoc = Loc.Localize("EstimatedRemainingDurationWithHours", "{0:00}:{1:00}:{2:00} remaining");
        }

        public string DownloadingMetaLoc { get; private set; }
        public string VerifyingLoc { get; private set; }
        public string RepairingLoc { get; private set; }
        public string ConnectingLoc { get; private set; }
        public string ReattemptWaitingLoc { get; private set; }
        public string FinishingLoc { get; private set; }
        public string CancelWithShortcutLoc { get; private set; }
        public string SpeedUnitPerSecLoc { get; private set; }
        public string EstimatedRemainingDurationLoc { get; private set; }
        public string EstimatedRemainingDurationWithHoursLoc { get; private set; }

        public ICommand CancelCommand { get; set; }

        public string FormatEstimatedTime(long remaining, long speed)
        {
            if (speed == 0)
                return string.Format(EstimatedRemainingDurationWithHoursLoc, 99, 59, 59);
            var remainingSecs = (int)Math.Ceiling(1.0 * remaining / speed);
            remainingSecs = Math.Min(remainingSecs, 60 * 60 * 100 - 1);
            if (remainingSecs < 60 * 60)
                return string.Format(EstimatedRemainingDurationLoc, remainingSecs / 60, remainingSecs % 60);
            else
                return string.Format(EstimatedRemainingDurationWithHoursLoc, remainingSecs / 60 / 60, remainingSecs / 60 % 60, remainingSecs % 60);
        }
    }
}