using CheapLoc;

namespace XIVLauncher.Windows.ViewModel
{
    class ChatChannelSetupViewModel
    {
        public ChatChannelSetupViewModel()
        {
            SetupLoc();
        }

        private void SetupLoc()
        {
            ChatChannelSetupTitleLoc = Loc.Localize("ChatChannelSetupTitle", "Configure chat channel");
            ChatChannelSetupDescriptionLoc = Loc.Localize("ChatChannelSetupDescription",
                "Please enter if XIVLauncher should post to a channel in a server or to an user in DMs,\r\nand their respective IDs.");
            HowLoc = Loc.Localize("HowToHint", "How do I set this up?");
            OkLoc = Loc.Localize("OK", "OK");
        }

        public string ChatChannelSetupTitleLoc { get; private set; }
        public string ChatChannelSetupDescriptionLoc { get; private set; }
        public string HowLoc { get; private set; }
        public string OkLoc { get; private set; }
    }
}
