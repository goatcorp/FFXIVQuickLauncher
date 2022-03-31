using System;

namespace XIVLauncher.Common.Game.Exceptions;

public class InvalidVersionFilesException : Exception
{
    public InvalidVersionFilesException()
        : base("Version files are invalid.")
    {
    }
}