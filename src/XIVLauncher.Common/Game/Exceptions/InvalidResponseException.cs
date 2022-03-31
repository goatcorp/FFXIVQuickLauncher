using System;

namespace XIVLauncher.Common.Game.Exceptions;

public class InvalidResponseException : Exception
{
    public string Document { get; set; }

    public InvalidResponseException(string message, string document)
        : base(message)
    {
        this.Document = document;
    }
}