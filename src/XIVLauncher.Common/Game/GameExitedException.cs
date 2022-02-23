using System;

namespace XIVLauncher.Common.Game
{
    public class GameExitedException : Exception
    {
        public GameExitedException() : base("Game exited prematurely.")
        { }
    }
}
