namespace XIVLauncher.Core.Components.SettingsPage;

public abstract class SettingsEntry : Component
{
    protected object? InternalValue;

    public abstract string Name { get; }

    public abstract void Load();

    public abstract void Save();
}