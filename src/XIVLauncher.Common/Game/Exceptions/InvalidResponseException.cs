using System;

namespace XIVLauncher.Common.Game.Exceptions;

public class InvalidResponseException : Exception
{
    public InvalidResponseException(string message)
        : base(message)
    {
    }
}