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
    private bool isDemoWindow = false;

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
        Loading,
        OtpEntry,
    }

    private LauncherState state = LauncherState.Main;

    public LauncherState State
    {
        get => this.state;
        set
        {
            this.state = value;

            switch (value)
            {
                case LauncherState.Settings:
                    this.setPage.OnShow();
                    break;

                case LauncherState.Main:
                    this.mainPage.OnShow();
                    break;

                case LauncherState.Loading:
                    this.loadingPage.OnShow();
                    break;

                case LauncherState.OtpEntry:
                    this.otpEntryPage.OnShow();
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(value), value, null);
            }
        }
    }

    private readonly MainPage mainPage;
    private readonly SettingsPage setPage;
    private readonly OtpEntryPage otpEntryPage;
    private readonly LoadingPage loadingPage;

    private readonly Background background = new();

    public LauncherApp(Storage storage)
    {
        this.storage = storage;

        this.mainPage = new MainPage(this);
        this.setPage = new SettingsPage(this);
        this.otpEntryPage = new OtpEntryPage(this);
        this.loadingPage = new LoadingPage(this);

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

    public bool HandleContinationBlocking(Task task)
    {
        if (task.IsFaulted)
        {
            this.OpenModalBlocking(task.Exception?.InnerException?.Message ?? "Unknown error - please check logs.", "Error");
            return false;
        }
        else if (task.IsCanceled)
        {
            this.OpenModalBlocking("Task was canceled.", "Error");
            return false;
        }

        return true;
    }

    public void AskForOtp()
    {
        this.otpEntryPage.Reset();
        this.State = LauncherState.OtpEntry;
    }

    public string? WaitForOtp()
    {
        while (this.otpEntryPage.Result == null && !this.otpEntryPage.Cancelled)
            Thread.Yield();

        return this.otpEntryPage.Result;
    }

    public void StartLoading(string line1)
    {
        this.State = LauncherState.Loading;
        this.loadingPage.Line1 = line1;
    }

    public void StopLoading()
    {
        this.State = LauncherState.Main;
    }

    public override void Draw()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2());

        ImGui.SetNextWindowPos(new Vector2(0, 0));
        ImGui.SetNextWindowSize(ImGuiHelpers.ViewportSize);

        if (ImGui.Begin("Background", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNavFocus | ImGuiWindowFlags.NoNavInputs | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            this.background.Draw();
        }

        ImGui.End();
        ImGui.PopStyleVar();

        ImGui.SetNextWindowPos(new Vector2(0, 0));
        ImGui.SetNextWindowSize(ImGuiHelpers.ViewportSize);
        ImGui.SetNextWindowBgAlpha(0.7f);

        if (ImGui.Begin("XIVLauncher", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            switch (State)
            {
                case LauncherState.Main:
                    this.mainPage.Draw();
                    break;

                case LauncherState.Settings:
                    this.setPage.Draw();
                    break;

                case LauncherState.OtpEntry:
                    this.otpEntryPage.Draw();
                    break;

                case LauncherState.Loading:
                    this.loadingPage.Draw();
                    break;
            }

            base.Draw();
        }

        if (IsDebug)
        {
            if (ImGui.IsKeyPressed(ImGuiKey.D) && ImGui.IsKeyPressed(ImGuiKey.E) && ImGui.IsKeyPressed(ImGuiKey.B))
            {
                this.isDemoWindow = true;
            }
        }

        ImGui.End();

        ImGui.PopStyleVar();

        if (this.isDemoWindow)
            ImGui.ShowDemoWindow(ref this.isDemoWindow);

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