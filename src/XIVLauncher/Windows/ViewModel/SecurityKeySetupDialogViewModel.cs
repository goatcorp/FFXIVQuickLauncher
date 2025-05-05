using CheapLoc;

namespace XIVLauncher.Windows.ViewModel
{
    class SecurityKeySetupDialogViewModel
    {
        public SecurityKeySetupDialogViewModel()
        {
            SetupLoc();
        }

        private void SetupLoc()
        {
            KeySetupInputPromptInsertLoc = Loc.Localize("SetupInputPromptInsert", "Please insert your YubiKey now.");
            KeySetupInputPromptYubiLoc = Loc.Localize("SetupInputPromptYubi", "Found a Yubikey.\nPlease enter your Authentication Key.");
            CancelWithShortcutLoc = Loc.Localize("CancelWithShortcut", "_Cancel");
            OkLoc = Loc.Localize("OK", "OK");
            KeySetupOnClickHintLoc = Loc.Localize("SetupOnClickHint", "Don't know what this is?\n Check out the FAQ!");
            KeySetupInputPromptBadLoc = Loc.Localize("SetupInputPromptBad", "Enter a valid Authentication Key.\nKey needs to be 32 characters long.");
            KeySetupCheckBoxLoc = Loc.Localize("SetupCheckBox", "Require Touch?");
            KeySetupTooltipLoc = Loc.Localize("SetupTooltip", "If checked, makes your YubiKey device require touch in order to authenticate.");
            KeySetupUsernameLoc = Loc.Localize("SetupUsername", "Unable to verify username\nPlease login using XIVLauncher at least once.");
        }
        public string KeySetupInputPromptInsertLoc { get; private set; }
        public string KeySetupInputPromptYubiLoc { get; private set; }
        public string CancelWithShortcutLoc { get; private set; }
        public string OkLoc { get; private set; }
        public string KeySetupOnClickHintLoc { get; private set; }
        public string KeySetupInputPromptBadLoc { get; private set; }
        public string KeySetupCheckBoxLoc { get; private set; }
        public string KeySetupTooltipLoc { get; private set; }
        public string KeySetupUsernameLoc { get; private set; }

    }
}
