using CheapLoc;

namespace XIVLauncher.Windows.ViewModel
{
    class OtpInputDialogViewModel
    {
        public OtpInputDialogViewModel()
        {
            SetupLoc();
        }

        private void SetupLoc()
        {
            OtpInputPromptLoc = Loc.Localize("OtpInputPrompt", "Please enter your OTP key.");
            CancelWithShortcutLoc = Loc.Localize("CancelWithShortcut", "_Cancel");
            OkLoc = Loc.Localize("OK", "OK");
            OtpOneClickHintLoc = Loc.Localize("OtpOneClickHint", "Or use the app!\r\nClick here to learn more!");
            OtpInputPromptBadLoc = Loc.Localize("OtpInputPromptBad", "Enter a valid OTP key.\nIt is 6 digits long.");
            PasteButtonLoc = Loc.Localize("PasteButton", "Click here to paste from the clipboard.");
        }

        public string OtpInputPromptLoc { get; private set; }
        public string CancelWithShortcutLoc { get; private set; }
        public string OkLoc { get; private set; }
        public string OtpOneClickHintLoc { get; private set; }
        public string OtpInputPromptBadLoc { get; private set; }
        public string PasteButtonLoc { get; private set; }
    }
}
