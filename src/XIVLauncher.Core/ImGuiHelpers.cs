using System.Numerics;
using ImGuiNET;

namespace XIVLauncher.Core;

public static class ImGuiHelpers
{
    public static Vector2 ViewportSize => ImGui.GetMainViewport().Size;

    public static float GlobalScale => ImGui.GetIO().FontGlobalScale;

    public static void CenteredText(string text)
    {
        CenterCursorForText(text);
        ImGui.TextUnformatted(text);
    }

    public static void CenterCursorForText(string text)
    {
        var textWidth = ImGui.CalcTextSize(text).X;
        CenterCursorFor((int)textWidth);
    }

    public static void CenterCursorFor(int itemWidth)
    {
        var window = (int)ImGui.GetWindowWidth();
        ImGui.SetCursorPosX(window / 2 - itemWidth / 2);
    }
}