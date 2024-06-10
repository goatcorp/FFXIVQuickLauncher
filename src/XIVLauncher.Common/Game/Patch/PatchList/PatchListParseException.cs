using System;

namespace XIVLauncher.Common.Game.Patch.PatchList;

public class PatchListParseException(string list, string message = null, Exception innerException = null) : Exception(message ?? "Failed to parse patch list", innerException)
{
    public string List { get; private set; } = list;
}
