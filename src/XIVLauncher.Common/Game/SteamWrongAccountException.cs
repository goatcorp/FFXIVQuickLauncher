using System;

namespace XIVLauncher.Common.Game;

public class SteamWrongAccountException : Exception
{
    public SteamWrongAccountException(string chosenUserName, string imposedUserName)
        : base($"Wrong username! chosen: {chosenUserName}, imposed: {imposedUserName}")
    {
    }
}