using System;

namespace XIVLauncher.Game
{
    public class InvalidResponseException : Exception
    {
        public InvalidResponseException(string message) : base(message)
        {
            
        }
    }
}