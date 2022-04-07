using XIVLauncher.Common.Dalamud;

namespace XIVLauncher.Core.Components.SettingsPage.Tabs;

public class SettingsTabDalamud : SettingsTab
{
    public override SettingsEntry[] Entries { get; } = {
        new SettingsEntry<bool>("Enable Dalamud", "Enable the Dalamud plugin system", () => Program.Config.DalamudEnabled ?? true, b => Program.Config.DalamudEnabled = b),

        new SettingsEntry<DalamudLoadMethod>("Load Method", "Choose how Dalamud is loaded.", () => Program.Config.DalamudLoadMethod ?? DalamudLoadMethod.DllInject, method => Program.Config.DalamudLoadMethod = method)
        {
            CheckValidity = x =>
            {
                if (x == DalamudLoadMethod.EntryPoint && !OperatingSystem.IsWindows())
                    return "Entry point injection is only supported on Windows.";

                return null;
            },
        },

        new NumericSettingsEntry("Injection Delay", "Choose how long to wait after the game has loaded before injecting.", () => Program.Config.DalamudLoadDelay, delay => Program.Config.DalamudLoadDelay = delay, 0, int.MaxValue, 1000),
    };

    public override string Title => "Dalamud";
}