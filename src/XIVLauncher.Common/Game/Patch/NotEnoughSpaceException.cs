using System;

namespace XIVLauncher.Common.Game.Patch;

public class NotEnoughSpaceException : Exception
{
    public enum SpaceKind
    {
        Patches,
        AllPatches,
        Game,
    }

    public SpaceKind Kind { get; private set; }

    public long BytesRequired { get; set; }

    public long BytesFree { get; set; }

    public NotEnoughSpaceException(SpaceKind kind, long required, long free)
    {
        this.Kind = kind;
        this.BytesRequired = required;
        this.BytesFree = free;
    }
}