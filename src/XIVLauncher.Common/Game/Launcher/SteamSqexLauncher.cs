using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

#if NET6_0_OR_GREATER && !WIN32
using System.Net.Security;
#endif

using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Serilog;
using XIVLauncher.Common.Encryption;
using XIVLauncher.Common.Game.Exceptions;
using XIVLauncher.Common.PlatformAbstractions;

#nullable enable

namespace XIVLauncher.Common.Game.Launcher;

public class SteamSqexLauncher : SqexLauncher
{
    private readonly ISteam? steam;
    private readonly byte[]? steamTicketData;
    private Ticket? steamTicket;

    public SteamSqexLauncher(ISteam? steam, IUniqueIdCache uniqueIdCache, ISettings settings)
        : base(uniqueIdCache, settings)
    {
        this.steam = steam;
    }

    public SteamSqexLauncher(byte[] steamTicketData, IUniqueIdCache uniqueIdCache, ISettings settings)
        : base(uniqueIdCache, settings)
    {
        this.steamTicketData = steamTicketData;
    }

    public override async Task<LoginResult> Login(string userName, string password, string otp, bool useCache, DirectoryInfo gamePath, bool forceBaseVersion, bool isFreeTrial)
    {
        Log.Information("SteamSqexLauncher::Login(cache:{UseCache})", useCache);

        if (this.steamTicketData != null)
        {
            this.steamTicket = Ticket.EncryptAuthSessionTicket(this.steamTicketData, (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            Log.Information("Using predefined steam ticket");
        }
        else
        {
            Debug.Assert(this.steam != null);

            try
            {
                if (!this.steam.IsValid)
                {
                    this.steam.Initialize(isFreeTrial ? Constants.STEAM_FT_APP_ID : Constants.STEAM_APP_ID);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not initialize Steam");
                throw new SteamException("SteamAPI_Init() failed.", ex);
            }

            if (!this.steam.IsValid)
            {
                throw new SteamException("Steam did not initialize successfully. Please restart Steam and try again.");
            }

            if (!this.steam.BLoggedOn)
            {
                throw new SteamException("Not logged into Steam, or Steam is running in offline mode. Please log in and try again.");
            }

            try
            {
                steamTicket = await Ticket.Get(steam).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                throw new SteamException("Could not request auth ticket.", ex);
            }
        }

        if (steamTicket == null)
        {
            throw new SteamException("Steam auth ticket was null.");
        }

        return await base.Login(userName, password, otp, useCache, gamePath, forceBaseVersion, isFreeTrial);
    }

    protected override void ModifyGameLaunchOptions(Dictionary<string, string> environment, ArgumentBuilder argumentBuilder)
    {
        Log.Information("SteamSqexLauncher::ModifyGameLaunchOptions(})");
        // These environment variable and arguments seems to be set when ffxivboot is started with "-issteam" (27.08.2019)
        environment.Add("IS_FFXIV_LAUNCH_FROM_STEAM", "1");
        argumentBuilder.Append("IsSteam", "1");
    }

    protected override async Task<(string Stored, string Text)> GetOauthTop(string url)
    {
        var topResult = await base.GetOauthTop(url);

        var steamRegex = new Regex(@"<input name=""sqexid"" type=""hidden"" value=""(?<sqexid>.*)""\/>");
        var steamMatches = steamRegex.Matches(topResult.Text);

        if (steamMatches.Count == 0)
        {
            Log.Error(topResult.Text);
            throw new InvalidResponseException("Could not get steam username.", topResult.Text);
        }

        var steamUsername = steamMatches[0].Groups["sqexid"].Value;

        return (topResult.Stored, steamUsername);
    }

    protected override string GetOauthTopUrl(int region, bool isFreeTrial)
    {
        var url = base.GetOauthTopUrl(region, isFreeTrial);
        url += "&issteam=1";

        url += $"&session_ticket={this.steamTicket.Text}";
        url += $"&ticket_size={this.steamTicket.Length}";

        return url;
    }

    protected override async Task<OauthLoginResult> OauthLogin(string userName, string password, string otp, bool isFreeTrial, int region)
    {
        if (this.steamTicket == null)
            throw new ArgumentNullException(nameof(this.steamTicket), "isSteam, but steamTicket == null");

        var topUrl = GetOauthTopUrl(region, isFreeTrial);
        var topResult = await GetOauthTop(topUrl);

        if (!String.Equals(userName, topResult.Text, StringComparison.OrdinalIgnoreCase))
            throw new SteamWrongAccountException(userName, topResult.Text);

        userName = topResult.Text;

        return await DoOauthLogin(topResult.Stored, topUrl, userName, password, otp);
    }
}

#nullable restore
