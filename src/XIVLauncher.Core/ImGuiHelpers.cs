using System.Numerics;
using ImGuiNET;

namespace XIVLauncher.Core;

public static class ImGuiHelpers
{
    public static Vector2 ViewportSize => ImGui.GetMainViewport().Size;

    public static float GlobalScale => ImGui.GetIO().FontGlobalScale;
}