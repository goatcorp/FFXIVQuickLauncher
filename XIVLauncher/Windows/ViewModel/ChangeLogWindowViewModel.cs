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
            SendEmailLoc = Loc.Localize("SendEmail", "Send Email");
            EmailInfoLoc = Loc.Localize("EmailInfo", "XIVLauncher is free, open-source software - it doesn't use any telemetry or analysis tools to collect your data, but it would help a lot if you could send me a short email with your operating system, why you use XIVLauncher and, if needed, any criticism or things we can do better. Your email will be deleted immediately after evaluation.\n\nThank you very much!");
            OkLoc = Loc.Localize("OK", "OK");
        }

        public string UpdateNoticeLoc { get; private set; }
        public string JoinDiscordLoc { get; private set; }
        public string SendEmailLoc { get; private set; }
        public string EmailInfoLoc { get; private set; }
        public string OkLoc { get; private set; }
    }
}
