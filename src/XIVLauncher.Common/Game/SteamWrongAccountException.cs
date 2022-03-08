using System;

namespace XIVLauncher.Common.Game;

public class SteamWrongAccountException : Exception
{
    public SteamWrongAccountException()
        : base("The Steam account you are logged into is not the same as the one linked to this account.")
    {
    }
}