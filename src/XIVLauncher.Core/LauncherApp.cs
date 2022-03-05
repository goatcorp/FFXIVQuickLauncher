using System.Numerics;
using ImGuiNET;
using XIVLauncher.Core.Components;
using XIVLauncher.Core.Components.MainPage;
using XIVLauncher.Core.Components.SettingsPage;

namespace XIVLauncher.Core;

public class LauncherApp : Component
{
    private readonly Storage storage;

    public static bool IsDebug { get; private set; } = false;

    #region Modal State

    private bool isModalDrawing = false;
    private bool modalOnNextFrame = false;
    private string modalText = string.Empty;
    private string modalTitle = string.Empty;
    private readonly ManualResetEvent modalWaitHandle = new(false);

    #endregion

    public enum LauncherState
    {
        Main,
        Settings,
        Progress,
    }

    public LauncherState State { get; set; } = LauncherState.Main;

    private readonly MainPage mainPage;
    private readonly SettingsPage setPage;

    public LauncherApp(Storage storage)
    {
        this.storage = storage;

        this.mainPage = new MainPage(this);
        this.setPage = new SettingsPage(this);

#if DEBUG
        IsDebug = true;
#endif
    }

    public void OpenModal(string text, string title)
    {
        if (this.isModalDrawing)
            throw new InvalidOperationException("Cannot open modal while another modal is open");

        this.modalText = text;
        this.modalTitle = title;
        this.isModalDrawing = true;
        this.modalOnNextFrame = true;
    }

    public void OpenModalBlocking(string text, string title)
    {
        if (!this.modalWaitHandle.WaitOne(0) && this.isModalDrawing)
            throw new InvalidOperationException("Cannot open modal while another modal is open");

        this.modalWaitHandle.Reset();
        this.OpenModal(text, title);
        this.modalWaitHandle.WaitOne();
    }

    public override void Draw()
    {
        ImGui.SetNextWindowPos(new Vector2(0, 0));

        if (ImGui.Begin("XIVLauncher", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize))
        {
            switch (State)
            {
                case LauncherState.Main:
                    this.mainPage.Draw();
                    break;

                case LauncherState.Settings:
                    this.setPage.Draw();
                    break;
            }

            base.Draw();
        }

        ImGui.End();

        this.DrawModal();
    }

    private void DrawModal()
    {
        if (ImGui.BeginPopupModal(this.modalTitle + "###xl_modal", ref this.isModalDrawing, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoScrollbar))
        {
            ImGui.TextWrapped(this.modalText);

            const float BUTTON_WIDTH = 120f;
            ImGui.SetCursorPosX((ImGui.GetWindowWidth() - BUTTON_WIDTH) / 2);

            if (ImGui.Button("OK", new Vector2(BUTTON_WIDTH, 40)))
            {
                ImGui.CloseCurrentPopup();
                this.isModalDrawing = false;
                this.modalWaitHandle.Set();
            }

            ImGui.EndPopup();
        }

        if (this.modalOnNextFrame)
        {
            ImGui.OpenPopup("###xl_modal");
            this.modalOnNextFrame = false;
            this.isModalDrawing = true;
        }
    }
}