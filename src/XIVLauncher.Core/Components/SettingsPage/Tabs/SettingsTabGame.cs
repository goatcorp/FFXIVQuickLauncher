using CheapLoc;
using ImGuiNET;
using Serilog;
using System.Numerics;
using XIVLauncher.Common;
using XIVLauncher.Common.Game;

namespace XIVLauncher.Core.Components.SettingsPage.Tabs;

public class SettingsTabGame : SettingsTab
{
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
                    
                    ImGui.OpenPopup("IntegrityCheck");
                    // Always center this window when appearing
                    Vector2 center = ImGui.GetMainViewport().GetCenter();
                    ImGui.SetNextWindowPos(center, ImGuiCond.Always, new Vector2(0.5f, 0.5f));
                }

                bool open = true;
                if (ImGui.BeginPopupModal("IntegrityCheck", ref open,
                    ImGuiWindowFlags.AlwaysAutoResize))
                {
                    ImGui.Text($"Begin integrity check?\n\nIt will take 5-10 minutes.");

                    if (ImGui.Button("Start"))
                    {
                        Log.Information("Running integrity check.");
                        var progress = new Progress<IntegrityCheck.IntegrityCheckProgress>();
                        progress.ProgressChanged += (sender, checkProgress) =>
                        {
                            #if DEBUG
                            Log.Debug($"Checking: {checkProgress.CurrentFile}");
                            #endif
                        };

                        string res = "";
                        Task.Run(async () => await IntegrityCheck.CompareIntegrityAsync(progress, Program.Config.GamePath)).ContinueWith(task =>
                        {
                            Log.Debug("Integrity check complete");
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
                        ImGui.CloseCurrentPopup();

                        Log.Information(res);

                    }
                    ImGui.SameLine();
                    if (ImGui.Button("Cancel"))
                    {
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.EndPopup();
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

    public override string Title => "Game";

    public override void Draw()
    {
        base.Draw();

        
    }
}