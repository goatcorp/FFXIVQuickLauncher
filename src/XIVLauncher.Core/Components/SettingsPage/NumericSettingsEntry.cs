namespace XIVLauncher.Core.Components.SettingsPage;

public class NumericSettingsEntry<T> : SettingsEntry<T>
{
    public long MinValue { get; set; }
    public long MaxValue { get; set; }

    public NumericSettingsEntry(string name, string description, Func<T?> load, Action<T?> save, long minValue = 0, long maxValue = long.MaxValue, long step = 1)
        : base(name, description, load, save)
    {
    }

    public override void Draw()
    {
        base.Draw();
    }
}