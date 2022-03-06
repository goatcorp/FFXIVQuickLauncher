using System;
using Steamworks;
using XIVLauncher.Common.PlatformAbstractions;

namespace XIVLauncher.Common.Windows
{
    public class WindowsSteam : ISteam
    {
        private static WindowsSteam instance;

        private WindowsSteam()
        {
            SteamUtils.OnGamepadTextInputDismissed += b => OnGamepadTextInputDismissed?.Invoke(b);
        }

        public static WindowsSteam Instance
        {
            get
            {
                instance ??= new WindowsSteam();
                return instance;
            }
        }

        public void Initialize(uint appId)
        {
            SteamClient.Init(appId);
        }

        public bool IsValid => SteamClient.IsValid && SteamClient.IsLoggedOn;

        public void Shutdown()
        {
            SteamClient.Shutdown();
        }

        public byte[] GetAuthSessionTicket()
        {
            return SteamUser.GetAuthSessionTicketAsync().GetAwaiter().GetResult().Data;
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

        public bool IsRunningOnSteamDeck()
        {
            //TODO(goat): Facepunch.Steamworks NuGet doesn't have this yet...
            return true;
        }

        public event Action<bool> OnGamepadTextInputDismissed;
    }
}