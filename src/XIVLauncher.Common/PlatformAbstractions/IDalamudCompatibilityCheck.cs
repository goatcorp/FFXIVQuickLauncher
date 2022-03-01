using System;

namespace XIVLauncher.Common.PlatformAbstractions;

public interface IDalamudCompatibilityCheck
{
    public void EnsureCompatibility();

    public class ArchitectureNotSupportedException : Exception
    {
        public ArchitectureNotSupportedException(string message)
            : base(message)
        {
        }
    }

    public class NoRedistsException : Exception
    {
    }
}