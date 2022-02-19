using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        }

        public string VerifyingLoc { get; private set; }
        public string RepairingLoc { get; private set; }
    }
}