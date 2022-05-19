using System;

namespace XIVLauncher.Common.Game.Exceptions;

public class SteamLinkNeededException : Exception
{
    public string? Document { get; set; }

    public SteamLinkNeededException(string document)
        : base("No steam account linked.")
    {
        this.Document = Document;
    }
}
