using XIVLauncher.Common;

namespace XIVLauncher.Core.Configuration.Linux;

public enum LinuxStartupType
{
    [SettingsDescription("Managed by XIVLauncher", "The game installation and wine setup is managed by XIVLauncher - you can leave it up to us.")]
    Managed,

    [SettingsDescription("Command", "Only use XIVLauncher to run a command with your login token and patch the game.")]
    Command,
}