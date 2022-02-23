using CheapLoc;

namespace XIVLauncher.Windows.ViewModel
{
    class GenericAddonSetupWindowViewModel
    {
        public GenericAddonSetupWindowViewModel()
        {
            SetupLoc();
        }

        private void SetupLoc()
        {
            GenericAddonSetupTitleLoc = Loc.Localize("GenericAddonSetupTitle", "Configure Auto-Start");
            GenericAddonSetupDescriptionLoc = Loc.Localize("GenericAddonSetupDescription",
                "Please select the application that should be started, any additional parameters and\r\nif it should be ran as admin.");
            CommandLineParametersLoc = Loc.Localize("CommandLineParameters", "Command line parameters");
            RunAsAdminLoc = Loc.Localize("RunAsAdminLoc", "Run as admin");
            RunOnCloseLoc = Loc.Localize("RunOnCloseLoc", "Run on game close");
            KillAfterCloseLoc = Loc.Localize("KillAfterCloseLoc", "Kill after game closes");
            OkLoc = Loc.Localize("OK", "OK");
        }

        public string GenericAddonSetupTitleLoc { get; private set; }
        public string GenericAddonSetupDescriptionLoc { get; private set; }
        public string CommandLineParametersLoc { get; private set; }
        public string RunAsAdminLoc { get; private set; }
        public string RunOnCloseLoc { get; private set; }
        public string KillAfterCloseLoc { get; private set; }
        public string OkLoc { get; private set; }
    }
}
