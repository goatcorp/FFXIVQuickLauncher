#nullable enable
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Steamworks;
using XIVLauncher.Common.PlatformAbstractions;

namespace XIVLauncher.Common.Unix;

/// <summary>
/// An implementation of ISteam for Unix endpoints. Borrows logic heavily from Facepunch's lib.
/// </summary>
public class UnixSteam : ISteam
{
    private Callback<GamepadTextInputDismissed_t>? textDismissedCallback;

    public void Initialize(uint appId)
    {
        // workaround because SetEnvironmentVariable doesn't actually touch the process environment on unix
        [System.Runtime.InteropServices.DllImport("c")]
        static extern int setenv(string name, string value, int overwrite);

        setenv("SteamAppId", appId.ToString(), 1);
        setenv("SteamGameId", appId.ToString(), 1);

        this.IsValid = SteamAPI.Init();

        this.textDismissedCallback = Callback<GamepadTextInputDismissed_t>.Create(this.GamepadTextInputCallback);
    }

    public bool IsValid { get; private set; }

    public bool BLoggedOn => SteamUser.BLoggedOn();

    public bool BOverlayNeedsPresent => SteamUtils.BOverlayNeedsPresent();

    public void Shutdown()
    {
        this.textDismissedCallback?.Dispose();
        SteamAPI.Shutdown();
        this.IsValid = false;
    }

    public async Task<byte[]?> GetAuthSessionTicketAsync()
    {
        var result = EResult.k_EResultPending;
        AuthTicket? ticket = null;
        var stopwatch = Stopwatch.StartNew();

        // We need to register our callback _before_ we can request our auth session ticket. Ignore the modified closure warning, this is what
        // we expect after all.
        using var cb = Callback<GetAuthSessionTicketResponse_t>.Create(t =>
        {
            // ReSharper disable AccessToModifiedClosure
            if (ticket == null || t.m_hAuthTicket != ticket.Handle) return;

            result = t.m_eResult;
        });

        ticket = this.GetAuthSessionTicket();
        if (ticket == null) return null;

        while (result == EResult.k_EResultPending)
        {
            await Task.Delay(10);

            if (stopwatch.Elapsed.TotalSeconds > 10)
            {
                ticket.Cancel();
                return null;
            }
        }

        if (result == EResult.k_EResultOK)
        {
            return ticket.Data;
        }

        ticket.Cancel();
        return null;
    }

    public bool IsAppInstalled(uint appId)
    {
        return SteamApps.BIsAppInstalled((AppId_t)appId);
    }

    public string GetAppInstallDir(uint appId)
    {
        SteamApps.GetAppInstallDir((AppId_t)appId, out var result, 1024);
        return result;
    }

    public bool ShowGamepadTextInput(bool password, bool multiline, string description, int maxChars, string existingText = "")
    {
        return SteamUtils.ShowGamepadTextInput(
            password ? EGamepadTextInputMode.k_EGamepadTextInputModePassword : EGamepadTextInputMode.k_EGamepadTextInputModeNormal,
            multiline ? EGamepadTextInputLineMode.k_EGamepadTextInputLineModeMultipleLines : EGamepadTextInputLineMode.k_EGamepadTextInputLineModeSingleLine,
            description,
            (uint)maxChars,
            existingText
        );
    }

    public string GetEnteredGamepadText()
    {
        var length = SteamUtils.GetEnteredGamepadTextLength();
        SteamUtils.GetEnteredGamepadTextInput(out var result, length);

        return result;
    }

    public bool ShowFloatingGamepadTextInput(ISteam.EFloatingGamepadTextInputMode mode, int x, int y, int width, int height)
    {
        return SteamUtils.ShowFloatingGamepadTextInput((EFloatingGamepadTextInputMode)mode, x, y, width, height);
    }

    public bool DismissFloatingGamepadTextInput()
    {
        return SteamUtils.DismissFloatingGamepadTextInput();
    }

    public bool IsRunningOnSteamDeck()
    {
        return SteamUtils.IsSteamRunningOnSteamDeck();
    }

    public uint GetServerRealTime()
    {
        return SteamUtils.GetServerRealTime();
    }

    public void ActivateGameOverlayToWebPage(string url, bool modal = false)
    {
        var mode = modal
                       ? EActivateGameOverlayToWebPageMode.k_EActivateGameOverlayToWebPageMode_Modal
                       : EActivateGameOverlayToWebPageMode.k_EActivateGameOverlayToWebPageMode_Default;

        SteamFriends.ActivateGameOverlayToWebPage(url, mode);
    }

    public event Action<bool>? OnGamepadTextInputDismissed;

    private void GamepadTextInputCallback(GamepadTextInputDismissed_t cb)
    {
        this.OnGamepadTextInputDismissed?.Invoke(cb.m_bSubmitted);
    }

    private AuthTicket? GetAuthSessionTicket()
    {
        var buffer = new byte[1024];
        var ticket = SteamUser.GetAuthSessionTicket(buffer, buffer.Length, out var ticketLength);

        if (ticket == HAuthTicket.Invalid) return null;

        return new AuthTicket
        {
            Data = buffer.Take((int)ticketLength).ToArray(),
            Handle = ticket
        };
    }

    private class AuthTicket : IDisposable
    {
        public byte[]? Data { get; set; }
        public HAuthTicket Handle { get; set; }

        public void Cancel()
        {
            if (this.Handle != HAuthTicket.Invalid)
            {
                SteamUser.CancelAuthTicket(this.Handle);
            }

            this.Handle = HAuthTicket.Invalid;
            this.Data = null;
        }

        public void Dispose()
        {
            this.Cancel();
        }
    }
}
