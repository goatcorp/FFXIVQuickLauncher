using System;

namespace XIVLauncher.Common.Patching.ZiPatch
{
    /// <summary>
    /// A ZiPatch exception.
    /// </summary>
    public class ZiPatchException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ZiPatchException"/> class.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception, or a null reference.</param>
        public ZiPatchException(string message = "ZiPatch error", Exception innerException = null) : base(message, innerException)
        {
        }
    }
}
