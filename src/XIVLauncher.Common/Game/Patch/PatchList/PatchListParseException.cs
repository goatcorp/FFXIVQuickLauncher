using System;

namespace XIVLauncher.Common.Game.Patch.PatchList;

public class PatchListParseException : Exception
{
    public string List { get; private set; }

    public PatchListParseException(string list, Exception innerException)
        : base("Failed to parse patch list", innerException)
    {
        List = list;
    }
}