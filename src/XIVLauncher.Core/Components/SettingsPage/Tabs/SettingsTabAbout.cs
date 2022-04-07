using System.Numerics;
using ImGuiNET;

namespace XIVLauncher.Core.Components.SettingsPage.Tabs;

public class SettingsTabAbout : SettingsTab
{
    private readonly TextureWrap logoTexture;

    public override SettingsEntry[] Entries { get; } =
    {
        new SettingsEntry<bool>("Use UID Cache", "Tries to save your login token for the next start.", () => Program.Config.IsUidCacheEnabled ?? false, x => Program.Config.IsUidCacheEnabled = x),
        new SettingsEntry<bool>("Encrypt Arguments", "Encrypt arguments to the game client.", () => Program.Config.IsEncryptArgs ?? false, x => Program.Config.IsEncryptArgs = x),
    };

    public override string Title => "About";

    public SettingsTabAbout()
    {
        this.logoTexture = TextureWrap.Load(AppUtil.GetEmbeddedResourceBytes("logo.png"));
    }

    public override void Draw()
    {
        ImGui.Text("This is XIVLauncher Core v" + AppUtil.GetAssemblyVersion());
        ImGui.Image(this.logoTexture.ImGuiHandle, new Vector2(256) * ImGuiHelpers.GlobalScale);

        base.Draw();
    }
}