using ImGuiNET;
using System.Numerics;

namespace XIVLauncher.Core.Components.MainPage;

public class NewsFrame : Component
{
    private readonly MainPage mainPage;

    public NewsFrame(MainPage mainPage)
    {
        this.mainPage = mainPage;
    }

    private Vector2 GetSize()
    {
        var vp = ImGuiHelpers.ViewportSize;
        var calculatedSize = vp.X >= 1280 ? vp.X * 0.7f : vp.X * 0.5f;
        return new Vector2(calculatedSize, vp.Y - 128f);
    }

    public override void Draw()
    {
        if (ImGui.BeginChild("###newsFrame", this.GetSize()))
        {
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(32f, 32f));

            ImGui.Text("awooga");

            ImGui.PopStyleVar();
        }

        ImGui.EndChild();

        base.Draw();
    }
}