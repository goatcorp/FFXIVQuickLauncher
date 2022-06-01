using System;

namespace XIVLauncher.Common.Dalamud;

public class DalamudRunnerException : Exception
{
     public DalamudRunnerException(string message, Exception innerException = null)
          : base(message, innerException)
     {
     }
}