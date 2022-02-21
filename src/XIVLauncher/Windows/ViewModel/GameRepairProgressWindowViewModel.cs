using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            VerifyingLoc = Loc.Localize("NowVerifying", "Verifying game files...");
            RepairingLoc = Loc.Localize("NowRepairing", "Repairing game files...");
            CancelLoc = Loc.Localize("Cancel", "Cancel");
            SpeedUnitPerSecLoc = Loc.Localize("SpeedUnitPerSecLoc", "{0}/s");
            EstimatedRemainingDurationLoc = Loc.Localize("EstimatedRemainingDuration", "{0:00}:{1:00} remaining");
            EstimatedRemainingDurationWithHoursLoc = Loc.Localize("EstimatedRemainingDurationWithHours", "{0:00}:{1:00}:{2:00} remaining");
        }

        public string VerifyingLoc { get; private set; }
        public string RepairingLoc { get; private set; }
        public string CancelLoc { get; private set; }
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