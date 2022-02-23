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
            ResetUidCacheHintLoc = Loc.Localize("ResetUidCacheHint", "Hold the control key to reset UID cache!");
        }

        public string UpdateCheckLoc { get; private set; }
        public string AutoLoginHintLoc { get; private set; }
        public string ResetUidCacheHintLoc { get; private set; }
    }
}
