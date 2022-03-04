using System.Numerics;
using ImGuiNET;
using XIVLauncher.Core.Components;

namespace XIVLauncher.Core;

public class App : Component
{
    private readonly Storage storage;

    private readonly LoginFrame loginFrame;

    public static bool IsDebug { get; private set; } = false;

    public App(Storage storage)
    {
        this.storage = storage;

#if DEBUG
        IsDebug = true;
#endif

        this.Children.Add(this.loginFrame = new LoginFrame
        {
            Margins = new(10, 10, 10, 10)
        });
    }

    public override void Draw()
    {
        ImGui.SetNextWindowPos(new Vector2(0, 0));

        if (ImGui.Begin("XIVLauncher", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Text("Hi");

            base.Draw();
        }

        ImGui.End();
    }
}