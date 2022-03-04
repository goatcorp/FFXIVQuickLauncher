using System.Numerics;
using ImGuiNET;

namespace XIVLauncher.Core;

public class ImGuiHelpers
{
    public Vector2 ViewportSize => ImGui.GetMainViewport().Size;

    public float GlobalScale => ImGui.GetIO().FontGlobalScale;
}