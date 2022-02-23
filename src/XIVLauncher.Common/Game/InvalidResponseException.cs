using System;

namespace XIVLauncher.Common.Game
{
    public class InvalidResponseException : Exception
    {
        public InvalidResponseException(string message) : base(message)
        {
            
        }
    }
}