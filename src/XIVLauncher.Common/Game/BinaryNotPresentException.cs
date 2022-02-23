using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XIVLauncher.Common.Game
{
    public class BinaryNotPresentException : Exception
    {
        public string Path { get; private set; }

        public BinaryNotPresentException(string path) : base("Game binary was not found")
        {
            Path = path;
        }
    }
}
