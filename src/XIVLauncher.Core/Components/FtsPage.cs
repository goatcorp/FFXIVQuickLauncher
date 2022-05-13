using System.Numerics;
using ImGuiNET;
using XIVLauncher.Common;

namespace XIVLauncher.Core.Components;

public class FtsPage : Page
{
    private readonly TextureWrap steamdeckFtsTexture;

    public FtsPage(LauncherApp app)
        : base(app)
    {
        this.steamdeckFtsTexture = TextureWrap.Load(AppUtil.GetEmbeddedResourceBytes("steamdeck_fts.png"));
    }

    // TODO: We don't have the steamworks api for this yet.
    private bool IsSteamDeck => Directory.Exists("/home/deck");

    public void OpenFtsIfNeeded()
    {
        if (!(App.Settings.CompletedFts ?? false) && IsSteamDeck)
            App.State = LauncherApp.LauncherState.Fts;
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

        ImGui.Image(this.steamdeckFtsTexture.ImGuiHandle, new Vector2(1280, 800));

        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Vector4.Zero);

        ImGui.SetCursorPos(new Vector2(316, 481));

        if (ImGui.Button("###openGuideButton", new Vector2(649, 101)))
        {
            Util.OpenBrowser("https://goatcorp.github.io/faq/steamdeck");
        }

        ImGui.SetCursorPos(new Vector2(316, 598));

        if (ImGui.Button("###finishFtsButton", new Vector2(649, 101)))
        {
            this.FinishFts(true);
        }

        ImGui.PopStyleColor(3);

        base.Draw();
    }
}