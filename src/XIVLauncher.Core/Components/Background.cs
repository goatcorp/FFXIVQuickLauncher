using System.Numerics;
using ImGuiNET;

namespace XIVLauncher.Core.Components;

public class Background : Component
{
    private TextureWrap bgTexture;

    public Background()
    {
        this.bgTexture = TextureWrap.Load(AppUtil.GetEmbeddedResourceBytes("bg_logo.png"));
    }

    public override void Draw()
    {
        ImGui.SetCursorPos(new Vector2(0, ImGuiHelpers.ViewportSize.Y - bgTexture.Height));

        ImGui.Image(bgTexture.ImGuiHandle, new Vector2(bgTexture.Width, bgTexture.Height));

        /*
        ImGui.SetCursorPos(new Vector2());

        var vpSize = ImGuiHelpers.ViewportSize;

        var width = vpSize.X;
        var height = this.bgTexture.Height / (float)this.bgTexture.Width * width;

        if (height < vpSize.Y)
        {
            height = vpSize.Y;
            width = this.bgTexture.Width / (float)this.bgTexture.Height * height;
            ImGui.SetCursorPosX((vpSize.X - width) / 2);
        }
        else
        {
            ImGui.SetCursorPosY((vpSize.Y - height) / 2);
        }

        ImGui.Image(this.bgTexture.ImGuiHandle, new Vector2(width, height));
        */

        base.Draw();
    }
}