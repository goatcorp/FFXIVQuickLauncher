using XIVLauncher.Common.Dalamud;

namespace XIVLauncher.Core.Components.SettingsPage.Tabs;

public class SettingsTabDalamud : SettingsTab
{
    public override SettingsEntry[] Entries { get; } = new[]
    {
        new SettingsEntry<DalamudLoadMethod>("Load Method", "Choose how Dalamud is loaded.", () => Program.Config.DalamudLoadMethod ?? DalamudLoadMethod.DllInject, method => Program.Config.DalamudLoadMethod = method)
        {
            CheckValidity = x =>
            {
                if (x == DalamudLoadMethod.EntryPoint && !OperatingSystem.IsWindows())
                    return "Entry point injection is only supported on Windows.";

                return null;
            },
        },
    };

    public override string Title => "Dalamud";
}