using System.Numerics;
using ImGuiNET;

namespace XIVLauncher.Core.Components;

public class SteamDeckPromptPage : Page
{
    private readonly TextureWrap updateWarnTexture;

    public SteamDeckPromptPage(LauncherApp app)
        : base(app)
    {
        this.updateWarnTexture = TextureWrap.Load(AppUtil.GetEmbeddedResourceBytes("steamdeck_switchprompt.png"));
    }

    public override void Draw()
    {
        ImGui.SetCursorPos(new Vector2(0));

        ImGui.Image(this.updateWarnTexture.ImGuiHandle, new Vector2(1280, 800));

        base.Draw();
    }
}