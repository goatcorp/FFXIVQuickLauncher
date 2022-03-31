using XIVLauncher.Common.Game.Patch.Acquisition;

namespace XIVLauncher.Core.Components.SettingsPage.Tabs;

public class SettingsTabPatching : SettingsTab
{
    public override SettingsEntry[] Entries { get; } =
    {
        new SettingsEntry<DirectoryInfo>("Patch Path", "Where patches should be downloaded to.", () => Program.Config.PatchPath, x => Program.Config.PatchPath = x)
        {
            CheckValidity = x =>
            {
                if (string.IsNullOrWhiteSpace(x?.FullName))
                    return "Patch path is not set.";

                return null;
            }
        },

        new SettingsEntry<AcquisitionMethod>("Patch Download Method", "How patches should be downloaded.", () => Program.Config.PatchAcquisitionMethod ?? AcquisitionMethod.Aria,
            x => Program.Config.PatchAcquisitionMethod = x),
        new NumericSettingsEntry("Maximum Speed", "Maximum download speed in bytes per second. Set to 0 for unlimited.", () => (int)Program.Config.PatchSpeedLimit,
            x => Program.Config.PatchSpeedLimit = x, 0, int.MaxValue, 1000),
        new SettingsEntry<bool>("Keep Patches", "Keep patches on disk after installing.", () => Program.Config.KeepPatches ?? false, x => Program.Config.KeepPatches = x),
    };

    public override string Title => "Patching";
}