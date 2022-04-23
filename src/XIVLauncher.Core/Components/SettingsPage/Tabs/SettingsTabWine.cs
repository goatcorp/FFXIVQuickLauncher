using System.Numerics;
using System.Xml.Schema;
using ImGuiNET;
using Serilog;
using XIVLauncher.Common;
using XIVLauncher.Common.Unix.Compatibility;

namespace XIVLauncher.Core.Components.SettingsPage.Tabs;

public class SettingsTabWine : SettingsTab
{
    private SettingsEntry<WineStartupType> startupTypeSetting;

    public SettingsTabWine()
    {
        Entries = new SettingsEntry[]
        {
            startupTypeSetting = new SettingsEntry<WineStartupType>("Installation Type", "Choose how XIVLauncher will start and manage your game installation.",
                () => Program.Config.WineStartupType ?? WineStartupType.Managed, x => Program.Config.WineStartupType = x),

            new SettingsEntry<string>("Startup Command Line",
                "Set the command XIVLauncher will run to start applications via wine. Here, you should specify things like your wineprefix. %COMMAND% is aliased to the EXE file and its arguments by XIVLauncher.",
                () => Program.Config.WineStartCommandLine, s => Program.Config.WineStartCommandLine = s)
            {
                CheckVisibility = () => startupTypeSetting.Value == WineStartupType.Command
            },

            new SettingsEntry<bool>("Enable GameMode", "Enable launching with GameMode optimizations.", () => Program.Config.GameModeEnabled ?? true, b => Program.Config.GameModeEnabled = b)
            {
                CheckValidity = b =>
                {
                    if (b == true && !File.Exists("/usr/lib/libgamemodeauto.so.0"))
                        return "GameMode not detected.";

                    return null;
                }
            },
            new SettingsEntry<bool>("Enable DXVK ASYNC", "Enable PROTON DXVK ASYNC patch.", () => Program.Config.DxvkAsyncEnabled ?? true, b => Program.Config.DxvkAsyncEnabled = b),
            new SettingsEntry<bool>("Enable ESync", "(Unimplemented) Enable eventfd-based synchronization.", () => Program.Config.ESyncEnabled ?? true, b => Program.Config.ESyncEnabled = b),
            new SettingsEntry<bool>("Enable FSync", "(Unimplemented)Enable fast user mutex (futex2).", () => Program.Config.FSyncEnabled ?? true, b => Program.Config.FSyncEnabled = b)
            {
                CheckValidity = b =>
                {
                    if (b == true && (Environment.OSVersion.Version.Major < 5 && (Environment.OSVersion.Version.Minor < 16 || Environment.OSVersion.Version.Major < 6)))
                        return "Kernel 5.16 or higher is required for FSync.";

                    return null;
                }
            },

            new SettingsEntry<Dxvk.DxvkHudType>("DXVK Overlay", "Configure how much of the DXVK overlay is to be shown.", () => Program.Config.DxvkHudType, type => Program.Config.DxvkHudType = type),
            new SettingsEntry<string>("WINEDEBUG Variables", "Configure debug logging for wine. Useful for troubleshooting.", () => Program.Config.WineDebugVars ?? string.Empty, s => Program.Config.WineDebugVars = s)
        };
    }

    public override SettingsEntry[] Entries { get; }

    public override bool IsUnixExclusive => true;

    public override string Title => "Wine";

    public override void Draw()
    {
        base.Draw();

        if (!Program.CompatibilityTools.IsToolDownloaded)
        {
            ImGui.BeginDisabled();
            ImGui.Text("Compatibility tool isn't set up. Please start the game at least once.");

            ImGui.Dummy(new Vector2(10));
        }

        if (ImGui.Button("Open prefix"))
        {
            Util.OpenBrowser(Program.CompatibilityTools.Prefix.FullName);
        }

        if (ImGui.Button("Open Wine configuration"))
        {
            Program.CompatibilityTools.RunInPrefix("C:/windows/system32/winecfg.exe");
        }

        if (ImGui.Button("Kill all wine processes"))
        {
            Program.CompatibilityTools.Kill();
        }

        if (!Program.CompatibilityTools.IsToolDownloaded)
        {
            ImGui.EndDisabled();
        }
    }
}
