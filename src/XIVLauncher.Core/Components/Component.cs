using System.Collections.Concurrent;

namespace XIVLauncher.Core.Components;

public abstract class Component
{
    public bool Enabled { get; set; } = true;

    public Margins Margins { get; set; } = new();

    public BlockingCollection<Component> Children { get; } = new();

    protected Guid Id { get; } = Guid.NewGuid();

    public virtual void Draw()
    {
        foreach (var child in Children)
        {
            if (child.Enabled)
                child.Draw();
        }
    }
}