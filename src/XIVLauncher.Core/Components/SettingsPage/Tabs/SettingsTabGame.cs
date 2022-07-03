using CheapLoc;
using ImGuiNET;
using Serilog;
using System.Numerics;
using XIVLauncher.Common;
using XIVLauncher.Common.Game;

namespace XIVLauncher.Core.Components.SettingsPage.Tabs;

public class SettingsTabGame : SettingsTab
{

    const float BUTTON_WIDTH = 120f;

    static bool runningIntegrity = false;

    #region Modal State

    private static bool isModalDrawing = false;
    private static bool modalOnNextFrame = false;
    private static string modalText = string.Empty;
    private static string modalTitle = string.Empty;
    private static readonly ManualResetEvent modalWaitHandle = new(false);

    #endregion

    public override SettingsEntry[] Entries { get; } =
    {
        new SettingsEntry<DirectoryInfo>("Game Path", "Where the game is installed to.", () => Program.Config.GamePath, x => Program.Config.GamePath = x)
        {
            CheckValidity = x =>
            {
                if (string.IsNullOrWhiteSpace(x?.FullName))
                    return "Game path is not set.";

                if (x.Name == "game" || x.Name == "boot")
                    return "Please select the path containing the folders \"game\" and \"boot\", not the folders itself.";

                string saveIntegrityPath = Path.Combine(Program.storage.Root.ToString(), "integrityreport.txt");

                if (ImGui.Button("Run Integrity Check"))
                {                    
#if DEBUG
                    Log.Information("Saving integrity to " + saveIntegrityPath);
#endif
                    
                    runningIntegrity = true;
                    ShowMessage("Running Integrity Check", "Integrity Check");
                    Log.Information("Running integrity check.");
                    RunIntegrity(saveIntegrityPath);
                }
                

                return null;
            }
        },


        new SettingsEntry<DirectoryInfo>("Game Config Path", "Where the user config files will be stored.", () => Program.Config.GameConfigPath, x => Program.Config.GameConfigPath = x)
        {
            CheckValidity = x => string.IsNullOrWhiteSpace(x?.FullName) ? "Game Config Path is not set." : null,

            // TODO: We should also support this on Windows
            CheckVisibility = () => Environment.OSVersion.Platform == PlatformID.Unix,
        },

        new SettingsEntry<bool>("Use DirectX11", "Use the modern DirectX11 version of the game.", () => Program.Config.IsDx11 ?? true, x => Program.Config.IsDx11 = x)
        {
            CheckWarning = x => !x ? "DirectX 9 is no longer supported by Square Enix or Dalamud. Things may not work." : null
        },

        new SettingsEntry<string>("Additional Arguments", "Additional args to start the game with", () => Program.Config.AdditionalArgs, x => Program.Config.AdditionalArgs = x),
        new SettingsEntry<ClientLanguage>("Game Language", "Select the game's language.", () => Program.Config.ClientLanguage ?? ClientLanguage.English, x => Program.Config.ClientLanguage = x),
        new SettingsEntry<DpiAwareness>("Game DPI Awareness", "Select the game's DPI Awareness. Change this if the game's scaling looks wrong.", () => Program.Config.DpiAwareness ?? DpiAwareness.Unaware, x => Program.Config.DpiAwareness = x),
        new SettingsEntry<bool>("Free trial account", "Check this if you are using a free trial account.", () => Program.Config.IsFt ?? false, x => Program.Config.IsFt = x),
        new SettingsEntry<bool>("Use XIVLauncher authenticator/OTP macros", "Check this if you want to use the XIVLauncher authenticator app or macros.", () => Program.Config.IsOtpServer ?? false, x => Program.Config.IsOtpServer = x),
    };

    private static void RunIntegrity(string saveIntegrityPath)
    {
        var progress = new Progress<IntegrityCheck.IntegrityCheckProgress>();
        progress.ProgressChanged += (sender, checkProgress) =>
        {
#if DEBUG
            Log.Debug($"Checking: {checkProgress.CurrentFile}");
#endif

            modalText = $"Checking: {checkProgress.CurrentFile}";
        };

        string res = "";
        Task.Run(async () => await IntegrityCheck.CompareIntegrityAsync(progress, Program.Config.GamePath)).ContinueWith(task =>
        {
            Log.Information("Integrity check completed");
            File.WriteAllText(saveIntegrityPath, task.Result.report);


            switch (task.Result.compareResult)
            {
                case IntegrityCheck.CompareResult.ReferenceNotFound:
                    res = Loc.Localize("IntegrityCheckImpossible",
                            "There is no reference report yet for this game version. Please try again later.");
                    break;

                case IntegrityCheck.CompareResult.ReferenceFetchFailure:
                    res = Loc.Localize("IntegrityCheckNetworkError",
                            "Failed to download reference files for checking integrity. Check your internet connection and try again.");
                    break;

                case IntegrityCheck.CompareResult.Invalid:
                    res = Loc.Localize("IntegrityCheckFailed",
                            "Some game files seem to be modified or corrupted. \n\nIf you use TexTools mods, this is an expected result.\n\nIf you do not use mods, right click the \"Login\" button on the XIVLauncher start page and choose \"Repair game\".");
                    break;

                case IntegrityCheck.CompareResult.Valid:
                    res = Loc.Localize("IntegrityCheckValid", "Your game install seems to be valid.");
                    break;
            }

        }).Wait();

        Log.Information(res);
        runningIntegrity = false;
        modalText = res;
        modalTitle = "Integrity Check Complete";
    }

    public override string Title => "Game";

    public override void Draw()
    {
        base.Draw();

        DrawModal();
    }

    private static void DrawModal()
    {
        ImGui.SetNextWindowSize(new Vector2(450, 300));

        if (ImGui.BeginPopupModal(modalTitle + "###xl_sp_modal", ref isModalDrawing, 
            ImGuiWindowFlags.AlwaysAutoResize | 
            ImGuiWindowFlags.NoScrollbar  |
            ImGuiWindowFlags.NoTitleBar
            )
        )
        {
            if (ImGui.BeginChild("###xl_modal_scrolling", new Vector2(0, -ImGui.GetTextLineHeightWithSpacing() * 2)))
            {
                ImGui.TextWrapped(modalText);
            }

            ImGui.EndChild();

            const float BUTTON_WIDTH = 120f;
            ImGui.SetCursorPosX((ImGui.GetWindowWidth() - BUTTON_WIDTH) / 2);

            
            if (!runningIntegrity)
            {
                if (ImGui.Button("OK", new Vector2(BUTTON_WIDTH, 40)))
                {
                    ImGui.CloseCurrentPopup();
                    isModalDrawing = false;
                    modalWaitHandle.Set();
                }
            }

            ImGui.EndPopup();
        }

        if (modalOnNextFrame)
        {
            ImGui.OpenPopup("###xl_sp_modal");
            modalOnNextFrame = false;
            isModalDrawing = true;
        }
    }

    public static void ShowMessage(string text, string title)
    {
        if (isModalDrawing)
            throw new InvalidOperationException("Cannot open modal while another modal is open");

        modalText = text;
        modalTitle = title;
        isModalDrawing = true;
        modalOnNextFrame = true;
    }

    public static void ShowMessageBlocking(string text, string title)
    {
        if (isModalDrawing)
            throw new InvalidOperationException("Cannot open modal while another modal is open");

        modalWaitHandle.Reset();
        ShowMessage(text, title);
        modalWaitHandle.WaitOne();
    }
}