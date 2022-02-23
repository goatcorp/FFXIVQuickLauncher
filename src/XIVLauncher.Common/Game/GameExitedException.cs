using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XIVLauncher.Common.Game
{
    public class GameExitedException : Exception
    {
        public GameExitedException() : base("Game exited prematurely.")
        { }
    }
}
