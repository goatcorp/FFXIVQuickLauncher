using System;

namespace XIVLauncher.Common.Dalamud;

public class DalamudRunnerException : Exception
{
     public DalamudRunnerException(string message)
          : base(message)
     {
     }
}