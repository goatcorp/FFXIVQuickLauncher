using ImGuiNET;
using System.Numerics;
using Serilog;

namespace XIVLauncher.Core.Components.Common;

public class Input : Component
{
    private string inputBacking = string.Empty;

    private volatile bool isSteamDeckInputActive = false;

    public string Label { get; }

    public string Hint { get; }

    public uint MaxLength { get; }

    public ImGuiInputTextFlags Flags { get; }

    public bool IsEnabled { get; set; } = true;
    public Vector2 Spacing { get; }

    public bool HasSteamDeckInput { get; set; }

    public string SteamDeckPrompt { get; set; }

    public bool TakeKeyboardFocus { get; set; }

    /** Executed on detection of the enter key **/
    public event Action? Enter;

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

        SteamDeckPrompt = hint;

        if (Program.Steam != null)
        {
            Program.Steam.OnGamepadTextInputDismissed += this.SteamOnOnGamepadTextInputDismissed;
            HasSteamDeckInput = Program.IsSteamDeckHardware;
        }
    }

    private void SteamOnOnGamepadTextInputDismissed(bool success)
    {
        if (success && this.isSteamDeckInputActive)
            this.inputBacking = Program.Steam!.GetEnteredGamepadText();

        this.isSteamDeckInputActive = false;
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

        if (TakeKeyboardFocus && ImGui.IsWindowAppearing())
            ImGui.SetKeyboardFocusHere();

        ImGui.Text(Label);

        if (!this.IsEnabled || this.isSteamDeckInputActive)
            ImGui.BeginDisabled();

        var ww = ImGui.GetWindowWidth();
        ImGui.SetNextItemWidth(ww);

        ImGui.PopStyleColor();

        ImGui.InputTextWithHint($"###{Id}", Hint, ref inputBacking, MaxLength, Flags);

        if (ImGui.IsItemFocused() && ImGui.IsKeyPressed(ImGuiKey.Enter))
        {
            Enter?.Invoke();
        }

        if (ImGui.IsItemActivated() && HasSteamDeckInput && Program.Steam != null && Program.Steam.IsValid)
        {
            this.isSteamDeckInputActive = Program.Steam?.ShowGamepadTextInput(Flags.HasFlag(ImGuiInputTextFlags.Password), false, SteamDeckPrompt, (int)MaxLength, this.inputBacking) ?? false;
            Log.Information("SteamDeck Input Active({Name}): {IsActive}", this.Label, this.isSteamDeckInputActive);
        }

        ImGui.Dummy(Spacing);

        if (!this.IsEnabled || this.isSteamDeckInputActive)
            ImGui.EndDisabled();

        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(4);
    }
}