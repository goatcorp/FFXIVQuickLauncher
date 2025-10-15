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

        // Injection Delay and Load Method localization
        InGameAddonInjectionDelayLoc = Loc.Localize("InGameAddonInjectionDelayLoc", "Addon Injection Delay (Legacy only)");
        InGameAddonInjectionDelayDescriptionLoc = Loc.Localize("InGameAddonInjectionDelayDescriptionLoc",
                                                               "Delay the injection of the in-game addon. This allows you to hide it from e.g. OBS and Discord, since they will inject before it does.");
        InGameAddonLoadMethodLoc = Loc.Localize("InGameAddonLoadMethodLoc", "Dalamud Load Method");
        InGameAddonLoadMethodEntryPointLoc = Loc.Localize("InGameAddonLoadMethodEntryPointLoc",
                                                          "New: improves compatibility with certain other software and plugins that need to load early.");
        InGameAddonLoadMethodDllInjectLoc = Loc.Localize("InGameAddonLoadMethodDllInjectLoc",
                                                         "Legacy: old version of the Dalamud injection system that may be more stable.");
        InGameAddonLoadMethodEntryPointDescriptionLoc = Loc.Localize("InGameAddonLoadMethodEntryPointDescriptionLoc",
                                                                     "This method uses Entry-Point rewriting to load Dalamud, which may be more compatible with certain other software, like anti-virus tools.");
        InGameAddonLoadMethodDllInjectDescriptionLoc = Loc.Localize("InGameAddonLoadMethodDllInjectDescriptionLoc",
                                                                    "This method loads Dalamud via DLL-injection, which may be more stable on certain systems.");
    }

    public string AdvancedSettingsTitleLoc { get; private set; }
    public string CloseLoc { get; private set; }

    public string EnabledUidCacheLoc { get; private set; }
    public string ResetUidCacheTipLoc { get; private set; }

    //public string EnableEncryptionLoc { get; private set; }

    public string ExitLauncherAfterGameExitLoc { get; private set; }
    public string TreatNonZeroExitCodeAsFailureLoc { get; private set; }
    public string ForceNorthAmericaLoc { get; private set; }

    // Injection Delay and Load Method localization
    public string InGameAddonInjectionDelayLoc { get; private set; }
    public string InGameAddonInjectionDelayDescriptionLoc { get; private set; }
    public string InGameAddonLoadMethodLoc { get; private set; }
    public string InGameAddonLoadMethodEntryPointLoc { get; private set; }
    public string InGameAddonLoadMethodDllInjectLoc { get; private set; }
    public string InGameAddonLoadMethodEntryPointDescriptionLoc { get; private set; }
    public string InGameAddonLoadMethodDllInjectDescriptionLoc { get; private set; }
}
