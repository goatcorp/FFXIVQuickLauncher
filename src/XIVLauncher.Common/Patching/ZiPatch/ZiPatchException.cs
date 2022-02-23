using System;

namespace XIVLauncher.Common.Patching.ZiPatch
{
    public class ZiPatchException : Exception
    {
        public ZiPatchException(string message = "ZiPatch error", Exception? innerException = null) : base(message, innerException)
        {
        }
    }
}