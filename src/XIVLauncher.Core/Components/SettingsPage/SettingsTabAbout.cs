using System.Numerics;
using ImGuiNET;

namespace XIVLauncher.Core.Components.SettingsPage;

public class SettingsTabAbout : SettingsTab
{
    private readonly TextureWrap logoTexture;

    public override string Title => "About";

    public SettingsTabAbout()
    {
        this.logoTexture = TextureWrap.Load(AppUtil.GetEmbeddedResourceBytes("logo.png"));
    }

    public override void Draw()
    {
        ImGui.Text("This is XIVLauncher Core v" + AppUtil.GetAssemblyVersion());
        ImGui.Image(this.logoTexture.ImGuiHandle, new Vector2(256) * ImGuiHelpers.GlobalScale);
    }

    public override void Load()
    {

    }

    public override void Save()
    {

    }
}