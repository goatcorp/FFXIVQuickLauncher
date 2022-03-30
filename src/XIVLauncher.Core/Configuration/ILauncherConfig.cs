using XIVLauncher.Common;
using XIVLauncher.Common.Dalamud;
using XIVLauncher.Core.Configuration.Linux;

namespace XIVLauncher.Core.Configuration;

public interface ILauncherConfig
{
    public string? CurrentAccountId { get; set; }
    public string? AcceptLanguage { get; set; }

    public DirectoryInfo? GamePath { get; set; }

    public string? AdditionalArgs { get; set; }

    public ClientLanguage? ClientLanguage { get; set; }

    public bool? UidCacheEnabled { get; set; }

    public float? GlobalScale { get; set; }

    public DpiAwareness? DpiAwareness { get; set; }

    #region Linux

    public LinuxStartupType? LinuxStartupType { get; set; }

    public string? LinuxStartCommandLine { get; set; }

    #endregion

    #region Dalamud

    public DalamudLoadMethod? DalamudLoadMethod { get; set; }

    #endregion
}