using CheapLoc;

namespace XIVLauncher.Windows.ViewModel;

public class AdvancedSettingsViewModel
{
    public AdvancedSettingsViewModel()
    {
        AdvancedSettingsTitleLoc = Loc.Localize("AdvancedSettings", "XIVLauncher Advanced Settings");
        CloseLoc = Loc.Localize("OK", "OK");

        EnabledUidCacheLoc = Loc.Localize("EnabledUidCache", "Enable experimental UID cache (this will break on game updates!)");
        ResetUidCacheTipLoc = Loc.Localize("ResetUidCacheTip", "Hold control while starting the launcher to reset the UID cache");
        //EnableEncryptionLoc = Loc.Localize("EnableEncryption", "Enable encrypting arguments to the client");
        ExitLauncherAfterGameExitLoc = Loc.Localize("ExitLauncherAfterGameExitLoc", "Exit XIVLauncher after game exit");
        TreatNonZeroExitCodeAsFailureLoc = Loc.Localize("TreatNonZeroExitCodeAsFailureLoc", "Treat non-zero game exit code as failure");
        ForceNorthAmericaLoc = Loc.Localize("ForceNorthAmerica", "Always download North American news, headlines and banners");
    }

    public string AdvancedSettingsTitleLoc { get; private set; }
    public string CloseLoc { get; private set; }

    public string EnabledUidCacheLoc { get; private set; }
    public string ResetUidCacheTipLoc { get; private set; }

    //public string EnableEncryptionLoc { get; private set; }

    public string ExitLauncherAfterGameExitLoc { get; private set; }
    public string TreatNonZeroExitCodeAsFailureLoc { get; private set; }
    public string ForceNorthAmericaLoc { get; private set; }
}
