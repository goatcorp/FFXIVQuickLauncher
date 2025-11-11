using System;

namespace XIVLauncher.Common.Game.Exceptions;

public class GameExitedException(int exitCode) : Exception("Game exited prematurely. Exit code: " + exitCode);
