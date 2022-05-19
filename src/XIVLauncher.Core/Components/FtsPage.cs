using System.Numerics;
using ImGuiNET;

namespace XIVLauncher.Core.Components;

public class FtsPage : Page
{
    private readonly TextureWrap steamdeckFtsTexture;
    private readonly TextureWrap steamdeckAppIdErrorTexture;

    private bool isSteamDeckAppIdError = false;

    public FtsPage(LauncherApp app)
        : base(app)
    {
        this.steamdeckFtsTexture = TextureWrap.Load(AppUtil.GetEmbeddedResourceBytes("steamdeck_fts.png"));
        this.steamdeckAppIdErrorTexture = TextureWrap.Load(AppUtil.GetEmbeddedResourceBytes("steamdeck_fterror.png"));
    }

    public void OpenFtsIfNeeded()
    {
        if (!(App.Settings.CompletedFts ?? false) && Program.IsSteamDeckHardware)
        {
            App.State = LauncherApp.LauncherState.Fts;
            return;
        }

        if (Program.IsSteamDeckHardware && (Program.Steam == null || !Program.Steam.IsValid))
        {
            App.State = LauncherApp.LauncherState.Fts;
            this.isSteamDeckAppIdError = true;
        }
    }

    private void FinishFts(bool save)
    {
        App.State = LauncherApp.LauncherState.Main;

        if (save)
            App.Settings.CompletedFts = true;
    }

    public override void Draw()
    {
        ImGui.SetCursorPos(new Vector2(0));

        ImGui.Image(this.isSteamDeckAppIdError ? this.steamdeckAppIdErrorTexture.ImGuiHandle : this.steamdeckFtsTexture.ImGuiHandle, new Vector2(1280, 800));

        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Vector4.Zero);

        ImGui.SetCursorPos(new Vector2(316, 481));

        if (ImGui.Button("###openGuideButton", new Vector2(649, 101)))
        {
            if (!this.isSteamDeckAppIdError)
            {
                AppUtil.OpenBrowser("https://goatcorp.github.io/faq/steamdeck");
            }
            else
            {
                Environment.Exit(0);
            }
        }

        ImGui.SetCursorPos(new Vector2(316, 598));

        if (ImGui.Button("###finishFtsButton", new Vector2(649, 101)) && !this.isSteamDeckAppIdError)
        {
            this.FinishFts(true);
        }

        ImGui.PopStyleColor(3);

        base.Draw();
    }
}