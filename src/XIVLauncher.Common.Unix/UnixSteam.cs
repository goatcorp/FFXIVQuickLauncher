using System;
using System.Threading.Tasks;
using Steamworks;
using XIVLauncher.Common.PlatformAbstractions;

namespace XIVLauncher.Common.Unix
{
    public class UnixSteam : ISteam
    {
        public UnixSteam()
        {
            SteamUtils.OnGamepadTextInputDismissed += b => OnGamepadTextInputDismissed?.Invoke(b);
        }

        public void Initialize(uint appId)
        {
            // workaround because SetEnvironmentVariable doesn't actually touch the process environment on unix
            [System.Runtime.InteropServices.DllImport("c")]
            static extern int setenv(string name, string value, int overwrite);

            setenv("SteamAppId", appId.ToString(), 1);
            setenv("SteamGameId", appId.ToString(), 1);

            SteamClient.Init(appId);
        }

        public bool IsValid => SteamClient.IsValid;

        public bool BLoggedOn => SteamClient.IsLoggedOn;

        public bool BOverlayNeedsPresent => SteamUtils.DoesOverlayNeedPresent;

        public void Shutdown()
        {
            SteamClient.Shutdown();
        }

        public async Task<byte[]?> GetAuthSessionTicketAsync()
        {
            var ticket = await SteamUser.GetAuthSessionTicketAsync().ConfigureAwait(true);
            return ticket?.Data;
        }

        public bool IsAppInstalled(uint appId)
        {
            return SteamApps.IsAppInstalled(appId);
        }

        public string GetAppInstallDir(uint appId)
        {
            return SteamApps.AppInstallDir(appId);
        }

        public bool ShowGamepadTextInput(bool password, bool multiline, string description, int maxChars, string existingText = "")
        {
            return SteamUtils.ShowGamepadTextInput(password ? GamepadTextInputMode.Password : GamepadTextInputMode.Normal, multiline ? GamepadTextInputLineMode.MultipleLines : GamepadTextInputLineMode.SingleLine, description, maxChars, existingText);
        }

        public string GetEnteredGamepadText()
        {
            return SteamUtils.GetEnteredGamepadText();
        }

        public bool ShowFloatingGamepadTextInput(ISteam.EFloatingGamepadTextInputMode mode, int x, int y, int width, int height)
        {
            SteamUtils.ShowFloatingGamepadTextInput((TextInputMode)mode, x, y, width, height);
            return true;
        }

        public bool IsRunningOnSteamDeck() => SteamUtils.IsRunningOnSteamDeck;

        public uint GetServerRealTime() => (uint)((DateTimeOffset)SteamUtils.SteamServerTime).ToUnixTimeSeconds();

        public void ActivateGameOverlayToWebPage(string url, bool modal = false)
        {
            SteamFriends.OpenWebOverlay(url, modal);
        }

        public event Action<bool> OnGamepadTextInputDismissed;
    }
}
