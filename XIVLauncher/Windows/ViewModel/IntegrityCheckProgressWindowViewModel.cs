using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CheapLoc;

namespace XIVLauncher.Windows.ViewModel
{
    class IntegrityCheckProgressWindowViewModel
    {
        public IntegrityCheckProgressWindowViewModel()
        {
            SetupLoc();
        }

        private void SetupLoc()
        {
            IntegrityCheckRunningLoc = Loc.Localize("IntegrityCheckRunning", "Running integrity check...");
        }

        public string IntegrityCheckRunningLoc { get; private set; }
    }
}
