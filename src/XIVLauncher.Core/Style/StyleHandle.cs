using System.Numerics;
using ImGuiNET;

namespace XIVLauncher.Core.Style;

public class StyleHandle : Dictionary<ImGuiStyleVar, object>, IDisposable
{
    public StyleHandle(bool applyNow = true)
    {
        if (applyNow)
            this.Push();
    }

    public void Push()
    {
        foreach (var entry in this)
        {
            switch (entry.Value)
            {
                case Vector2 vec2:
                    ImGui.PushStyleVar(entry.Key, vec2);
                    break;

                case float num:
                    ImGui.PushStyleVar(entry.Key, num);
                    break;
            }
        }
    }

    public void Pop()
    {
        ImGui.PopStyleVar(this.Count);
    }

    public void Dispose()
    {
        this.Pop();
    }
}