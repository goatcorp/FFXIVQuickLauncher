using System.Numerics;
using ImGuiNET;

namespace XIVLauncher.Core.Components.SettingsPage;

public abstract class SettingsTab : Component
{
    public abstract SettingsEntry[] Entries { get; }

    public virtual bool IsUnixExclusive => false;

    public abstract string Title { get; }

    public override void Draw()
    {
        foreach (SettingsEntry settingsEntry in Entries)
        {
            if (settingsEntry.IsVisible)
                settingsEntry.Draw();

            ImGui.Dummy(new Vector2(10) * ImGuiHelpers.GlobalScale);
        }

        base.Draw();
    }

    public void Load()
    {
        foreach (SettingsEntry settingsEntry in Entries)
        {
            settingsEntry.Load();
        }
    }

    public virtual void Save()
    {
        foreach (SettingsEntry settingsEntry in Entries)
        {
            settingsEntry.Save();
        }
    }
}