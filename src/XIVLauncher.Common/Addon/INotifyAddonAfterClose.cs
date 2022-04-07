namespace XIVLauncher.Common.Addon
{
    interface INotifyAddonAfterClose : IAddon
    {
        void GameClosed();
    }
}