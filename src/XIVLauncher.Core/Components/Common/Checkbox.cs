using ImGuiNET;
using System.Numerics;

namespace XIVLauncher.Core.Components.Common;

public class Checkbox : Component
{
    private bool inputBacking = false;

    public string Label { get; }

    public bool IsEnabled { get; set; } = true;

    public bool Value
    {
        get => inputBacking;
        set => inputBacking = value;
    }

    public event Action<bool> OnChange;

    public Checkbox(string label, bool value = false, bool isEnabled = true)
    {
        Label = label;
        Value = value;
        IsEnabled = isEnabled;
    }

    public override void Draw()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0.5f, 0.5f));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, ImGuiColors.BlueShade1);
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, ImGuiColors.BlueShade2);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, ImGuiColors.BlueShade2);
        ImGui.PushStyleColor(ImGuiCol.TextDisabled, ImGuiColors.TextDisabled);
        ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.Text);

        if (!this.IsEnabled)
            ImGui.BeginDisabled();

        if (ImGui.Checkbox($"###{Id}", ref inputBacking))
        {
            this.OnChange?.Invoke(this.inputBacking);
        }

        if (!this.IsEnabled)
            ImGui.EndDisabled();

        ImGui.SameLine();

        ImGui.Text(Label);

        if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
        {
            this.inputBacking = !this.inputBacking;
            this.OnChange?.Invoke(this.inputBacking);
        }

        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(5);
    }
}