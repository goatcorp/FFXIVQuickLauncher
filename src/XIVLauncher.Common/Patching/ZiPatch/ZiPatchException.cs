using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XIVLauncher.Common.Patching.ZiPatch
{
    public class ZiPatchException : Exception
    {
        public ZiPatchException(string message = "ZiPatch error", Exception innerException = null) : base(message, innerException)
        {
        }
    }
}