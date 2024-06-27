using CheapLoc;

namespace XIVLauncher.Windows.ViewModel
{
    class FirstTimeSetupViewModel
    {
        public FirstTimeSetupViewModel()
        {
            SetupLoc();
        }

        public void SetupLoc()
        {
            FirstTimeGamePathLoc = Loc.Localize("ChooseGamePathFTS",
                "Please select the folder your game is installed in.\r\nIt should contain the folders \"game\" and \"boot\".\n\nIf you don't have the game installed, you can choose an empty folder and XIVLauncher will install it for you.");
            FirstTimeSteamNoticeLoc = Loc.Localize("FirstTimeSteamNotice",
                "Please check this box if you are usually launching the game using Steam, or if you have an account with a Steam license.");
            FirstTimeSteamCheckBoxLoc = Loc.Localize("FirstTimeSteamCheckBox", "Enable Steam integration");
            FirstTimeLanguageLoc = Loc.Localize("ChooseLanguageFTS", "Please select which language you want to load the game with.");
            NextLoc = Loc.Localize("Next", "Next");
            FirstTimeDalamudLoc = Loc.Localize("FirstTimeDalamudNotice",
                "Do you want to enable Dalamud?\r\nThis will add some extra functionality to your game, such as RMT chat filtering and Discord notifications for chat messages or Duty Finder pops.\r\n\r\nTo configure these settings, please use the XIVLauncher settings menu and switch to the \"Dalamud\" tab.\r\nEnabling this, however, could cause a false positive in your antivirus software, please check its settings and add any needed exclusions if you run into problems.");
            FirstTimeDalamudCheckBoxLoc = Loc.Localize("FirstTimeDalamudCheckBox", "Enable Dalamud");
        }

        public string FirstTimeGamePathLoc { get; private set; }
        public string FirstTimeSteamNoticeLoc { get; private set; }
        public string FirstTimeSteamCheckBoxLoc { get; private set; }
        public string FirstTimeLanguageLoc { get; private set; }
        public string NextLoc { get; private set; }
        public string FirstTimeDalamudLoc { get; private set; }
        public string FirstTimeDalamudCheckBoxLoc { get; private set; }
    }
}
