namespace XIVLauncher.Common.Game.Patch.Acquisition
{
    public enum AcquisitionMethod
    {
        [SettingsDescription(".NET", "Basic .NET downloads")]
        NetDownloader,

        [SettingsDescription("Aria2c", "Aria2c downloads (recommended)")]
        Aria,
    }
}
