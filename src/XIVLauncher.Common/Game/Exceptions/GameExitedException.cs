using System;

namespace XIVLauncher.Common.Game.Exceptions;

public class GameExitedException : Exception
{
    public GameExitedException()
        : base("Game exited prematurely.")
    {
    }
}