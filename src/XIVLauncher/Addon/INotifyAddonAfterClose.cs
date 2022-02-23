namespace XIVLauncher.Addon
{
    interface INotifyAddonAfterClose : IAddon
    {
        void GameClosed();
    }
}
