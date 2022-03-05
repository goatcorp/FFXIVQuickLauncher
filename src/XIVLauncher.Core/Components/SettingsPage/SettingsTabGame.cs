using ImGuiNET;

namespace XIVLauncher.Core.Components.SettingsPage;

public class SettingsTabGame : SettingsTab
{
    private string? gamePath;
    private string? gameArguments;

    public override string Title => "Game";

    public override void Draw()
    {
        ImGui.InputText("Game Path", ref this.gamePath, 10000);
        ImGui.InputText("Game Arguments", ref this.gameArguments, 1000);
    }

    public override void Load()
    {
        this.gamePath = Program.Config.GamePath?.FullName;
        this.gameArguments = Program.Config.AdditionalArgs;

        this.gamePath ??= string.Empty;
        this.gameArguments ??= string.Empty;
    }

    public override void Save()
    {
        Program.Config.GamePath = new DirectoryInfo(this.gamePath);
        Program.Config.AdditionalArgs = this.gameArguments;
    }
}