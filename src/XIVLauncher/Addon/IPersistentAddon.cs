namespace XIVLauncher.Addon
{
    interface IPersistentAddon : IAddon
    { 
        void DoWork(object state);
    }
}
