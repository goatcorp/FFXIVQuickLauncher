using System.Numerics;
using ImGuiNET;

namespace XIVLauncher.Core.Components.LoadingPage;

public class Spinner : Component
{
    private readonly float radius;
    private readonly int thickness;
    private readonly uint color;

    public Spinner(float radius, int thickness, uint color)
    {
        this.radius = radius;
        this.thickness = thickness;
        this.color = color;
    }

    public override void Draw()
    {
        var framePadding = ImGui.GetStyle().FramePadding;

        var pos = ImGui.GetCursorPos();

        var time = ImGui.GetTime() / 1.2;

        const int NUM_SEGMENTS = 30;
        var start = Math.Abs(Math.Sin(time * 1.8f) * (NUM_SEGMENTS - 5));

        var aMin = Math.PI * 2.0f * ((float)start) / (float)NUM_SEGMENTS;
        var aMax = Math.PI * 2.0f * ((float)NUM_SEGMENTS - 3) / (float)NUM_SEGMENTS;

        var centre = new Vector2(pos.X + radius, pos.Y + radius + framePadding.Y);

        var drawList = ImGui.GetWindowDrawList();

        for (var i = 0; i < NUM_SEGMENTS; i++)
        {
            var a = aMin + (i / (float)NUM_SEGMENTS) * (aMax - aMin);
            drawList.PathLineTo(new Vector2((float)(centre.X + Math.Cos(a + time * 8) * radius),
                (float)(centre.Y + Math.Sin(a + time * 8) * radius)));
        }

        drawList.PathStroke(this.color, ImDrawFlags.RoundCornersAll, thickness);

        base.Draw();
    }
}