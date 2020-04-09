using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CheapLoc;

namespace XIVLauncher.Windows.ViewModel
{
    class UpdateLoadingDialogViewModel
    {
        public UpdateLoadingDialogViewModel()
        {
            SetupLoc();
        }

        public void SetupLoc()
        {
            UpdateCheckLoc = Loc.Localize("UpdateCheckMsg", "Checking for updates...");
            AutoLoginHintLoc = Loc.Localize("AutoLoginHint", "Hold the shift key to change settings!");
        }

        public string UpdateCheckLoc { get; private set; }
        public string AutoLoginHintLoc { get; private set; }
    }
}
