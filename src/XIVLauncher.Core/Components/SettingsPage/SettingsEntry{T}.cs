using ImGuiNET;
using XIVLauncher.Common;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Core.Components.SettingsPage;

public class SettingsEntry<T> : SettingsEntry
{
    private Guid Id { get; } = Guid.NewGuid();

    private readonly Func<T?> load;
    private readonly Action<T?> save;

    public override string Name { get; }

    public string Description { get; }

    public Func<T?, string?>? CheckValidity { get; init; }

    public Func<T?, string?>? CheckWarning { get; init; }

    public Func<bool>? CheckVisibility { get; init; }

    public override bool IsVisible => CheckVisibility?.Invoke() ?? true;

    public T? Value => this.InternalValue == default ? default : (T)this.InternalValue;

    public SettingsEntry(string name, string description, Func<T?> load, Action<T?> save)
    {
        this.load = load;
        this.save = save;
        this.Name = name;
        this.Description = description;
    }

    public override void Draw()
    {
        var type = typeof(T);

        if (type == typeof(DirectoryInfo))
        {
            ImGuiHelpers.TextWrapped(this.Name);

            var value = this.Value as DirectoryInfo;
            var nativeBuffer = value?.FullName ?? string.Empty;

            if (ImGui.InputText($"###{Id.ToString()}", ref nativeBuffer, 10000))
            {
                this.InternalValue = !string.IsNullOrEmpty(nativeBuffer) ? new DirectoryInfo(nativeBuffer) : null;
            }
        }
        else if (type == typeof(string))
        {
            ImGuiHelpers.TextWrapped(this.Name);

            var nativeBuffer = this.Value as string ?? string.Empty;

            if (ImGui.InputText($"###{Id.ToString()}", ref nativeBuffer, 1000))
            {
                this.InternalValue = nativeBuffer;
            }
        }
        else if (type == typeof(bool))
        {
            var nativeValue = this.Value as bool? ?? false;

            if (ImGui.Checkbox($"{Name}###{Id.ToString()}", ref nativeValue))
            {
                this.InternalValue = nativeValue;
            }
        }
        else if (type.IsEnum)
        {
            ImGuiHelpers.TextWrapped(this.Name);

            var idx = (int)(this.InternalValue ?? 0);
            var values = Enum.GetValues(type);
            var descriptions = values.Cast<Enum>().Select(x => x.GetAttribute<SettingsDescriptionAttribute>() ?? new SettingsDescriptionAttribute(x.ToString(), string.Empty)).ToArray();

            if (ImGui.BeginCombo($"###{Id.ToString()}", descriptions[idx].FriendlyName))
            {
                foreach (int value in values)
                {
                    if (ImGui.Selectable(descriptions[value].FriendlyName, idx == value))
                    {
                        this.InternalValue = value;
                    }
                }

                ImGui.EndCombo();
            }
        }

        ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudGrey);
        ImGuiHelpers.TextWrapped(this.Description);
        ImGui.PopStyleColor();

        if (this.CheckValidity != null)
        {
            var validityMsg = this.CheckValidity.Invoke(this.Value);
            this.IsValid = string.IsNullOrEmpty(validityMsg);

            if (!this.IsValid)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudRed);
                ImGui.Text(validityMsg);
                ImGui.PopStyleColor();
            }
        }
        else
        {
            this.IsValid = true;
        }

        var warningMessage = this.CheckWarning?.Invoke(this.Value);

        if (warningMessage != null)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudRed);
            ImGui.Text(warningMessage);
            ImGui.PopStyleColor();
        }

        base.Draw();
    }

    public override void Load()
    {
        this.InternalValue = this.load();

        if (this.CheckValidity != null)
        {
            this.IsValid = this.CheckValidity(this.Value) == null;
        }
        else
        {
            this.IsValid = true;
        }
    }

    public override void Save() => this.save(this.Value);
}