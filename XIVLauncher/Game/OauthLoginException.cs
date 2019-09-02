using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XIVLauncher.Game
{
    [Serializable]
    class OauthLoginException : Exception
    {
        public OauthLoginException(string message) : base(message)
        {
        }
    }
}
