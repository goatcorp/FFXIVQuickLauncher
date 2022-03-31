using System;

namespace XIVLauncher.Common.Game.Exceptions;

public class SteamLinkNeededException : Exception
{
    public SteamLinkNeededException()
        : base("No steam account linked.")
    {
    }
}