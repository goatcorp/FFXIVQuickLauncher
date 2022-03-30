using ImGuiNET;
using System.Numerics;

namespace XIVLauncher.Core.Components.Common
{
    public class Input : Component
    {
        private string inputBacking = string.Empty;

        public string Label { get; }

        public string Hint { get; }

        public uint MaxLength { get; }

        public ImGuiInputTextFlags Flags { get; }

        public bool IsEnabled { get; set; } = true;
        public Vector2 Spacing { get; }

        public string Value
        {
            get => inputBacking; 
            set => inputBacking = value;
        }

        public Input(
            string label,
            string hint,
            Vector2? spacing,
            uint maxLength = 255,
            bool isEnabled = true,
            ImGuiInputTextFlags flags = ImGuiInputTextFlags.None)
        {
            Label = label;
            Hint = hint;
            MaxLength = maxLength;
            Flags = flags;
            IsEnabled = isEnabled;
            Spacing = spacing ?? Vector2.Zero;
        }

        public override void Draw()
        {
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(12f, 10f));
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0);
            ImGui.PushStyleColor(ImGuiCol.FrameBg, ImGuiColors.BlueShade1);
            ImGui.PushStyleColor(ImGuiCol.FrameBgActive, ImGuiColors.BlueShade2);
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, ImGuiColors.BlueShade2);
            ImGui.PushStyleColor(ImGuiCol.TextDisabled, ImGuiColors.TextDisabled);
            ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.Text);

            ImGui.Text(Label);

            if (!this.IsEnabled)
                ImGui.BeginDisabled();

            var ww = ImGui.GetWindowWidth();
            ImGui.SetNextItemWidth(ww);

            ImGui.InputTextWithHint($"###{Id}", Hint, ref inputBacking, MaxLength, Flags);
            ImGui.Dummy(Spacing);

            if (!this.IsEnabled)
                ImGui.EndDisabled();
            
            ImGui.PopStyleVar(2);
            ImGui.PopStyleColor(5);
        }
    }
}
