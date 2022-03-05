using System.Numerics;
using ImGuiNET;

namespace XIVLauncher.Core.Components.SettingsPage;

public class SettingsPage : Page
{
    private readonly TextureWrap logoTexture;

    public SettingsPage(LauncherApp app)
        : base(app)
    {
        this.logoTexture = TextureWrap.Load(AppUtil.GetEmbeddedResourceBytes("logo.png"));
    }

    public override void Draw()
    {
        ImGui.Image(this.logoTexture.ImGuiHandle, new Vector2(128, 128));

        base.Draw();
    }
}