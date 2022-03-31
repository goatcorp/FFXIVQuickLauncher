namespace XIVLauncher.Core.Components.SettingsPage;

public abstract class SettingsEntry : Component
{
    protected object? InternalValue;

    public abstract string Name { get; }

    public bool IsValid { get; protected set; }

    public abstract bool IsVisible { get; }

    public abstract void Load();

    public abstract void Save();
}