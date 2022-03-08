using System;

namespace XIVLauncher.Common.Game;

public class SteamLinkNeededException : Exception
{
    public SteamLinkNeededException()
        : base("No steam account linked.")
    {
    }
}