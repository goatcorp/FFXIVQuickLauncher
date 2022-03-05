namespace XIVLauncher.Core.Components.SettingsPage;

public abstract class SettingsTab : Component
{
    public virtual bool IsLinux => false;

    public abstract string Title { get; }

    public abstract void Load();

    public abstract void Save();
}