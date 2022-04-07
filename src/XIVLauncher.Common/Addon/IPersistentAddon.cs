namespace XIVLauncher.Common.Addon
{
    interface IPersistentAddon : IAddon
    {
        void DoWork(object state);
    }
}