namespace XIVLauncher.Common.Game.Patch.Acquisition
{
    public enum AcquisitionMethod
    {
        [SettingsDescription(".NET", "Basic .NET downloads")]
        NetDownloader,

        [SettingsDescription("Torrent (+ .NET)", "Torrent downloads, with .NET as a fallback")]
        MonoTorrentNetFallback,

        [SettingsDescription("Torrent (+ Aria)", "Torrent downloads, with Aria as a fallback")]
        MonoTorrentAriaFallback,

        [SettingsDescription("Aria2c", "Aria2c downloads (recommended)")]
        Aria,
    }
}