namespace XIVLauncher.Common.Addon
{
    public interface IAddon
    {
        string Name { get; }

        void Setup(int gamePid);
    }
}