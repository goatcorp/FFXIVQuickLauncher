using System;

namespace XIVLauncher.Common.Game.Exceptions;

public class VersionCheckLoginException : Exception
{
    public LoginState State { get; }

    public VersionCheckLoginException(LoginState state)
        : base()
    {
        State = state;
    }
}
