using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CheapLoc;

namespace XIVLauncher.Windows.ViewModel
{
    class ChangeLogWindowViewModel
    {
        public ChangeLogWindowViewModel()
        {
            SetupLoc();
        }

        public void SetupLoc()
        {
            UpdateNoticeLoc = string.Format(Loc.Localize("UpdateNotice", "XIVLauncher was updated to version {0}."), Util.GetAssemblyVersion());
            JoinDiscordLoc = Loc.Localize("JoinDiscord", "Join Discord");
            OkLoc = Loc.Localize("OK", "OK");
        }

        public string UpdateNoticeLoc { get; private set; }
        public string JoinDiscordLoc { get; private set; }
        public string OkLoc { get; private set; }
    }
}
