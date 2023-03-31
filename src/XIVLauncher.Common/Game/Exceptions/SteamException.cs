using System;

namespace XIVLauncher.Common.Game.Exceptions;

public class SteamException : Exception
{
    public SteamException(string message, Exception innerException = null)
        : base(message, innerException)
    {
    }
}

public class SteamTicketNullException : SteamException
{
    public SteamTicketNullException()
        : base("Steam ticket was null.")
    {
    }
}