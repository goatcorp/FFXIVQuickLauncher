using System;

namespace XIVLauncher.Common.Game;

public class SteamException : Exception
{
    public SteamException(string message, Exception innerException = null)
        : base(message, innerException)
    {
    }
}