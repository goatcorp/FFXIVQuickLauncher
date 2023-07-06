using System;

namespace XIVLauncher.Common.Game.Exceptions;

public class SteamWrongAccountException : Exception
{
    public string ChosenUserName { get; private set; }

    public string ImposedUserName { get; private set; }

    public SteamWrongAccountException(string chosenUserName, string imposedUserName)
        : base($"Wrong username! chosen: {chosenUserName}, imposed: {imposedUserName}")
    {
        ChosenUserName = chosenUserName;
        ImposedUserName = imposedUserName;
    }
}