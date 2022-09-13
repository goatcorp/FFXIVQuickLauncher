namespace XIVLauncher.Core;

public class ConfigStorage
{
    public DirectoryInfo XLConfigRoot { get; }
    public DirectoryInfo GameConfigRoot { get; }

    private readonly DirectoryInfo symlinkGameConfigs;

    public ConfigStorage(string configFolder)
    {
        // https://developers.redhat.com/blog/2018/11/07/dotnet-special-folder-api-linux
        this.XLConfigRoot = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher"));
        this.GameConfigRoot = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher/ffxivConfig"));
        this.symlinkGameConfigs = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents/My Games/FINAL FANTASY XIV - A Realm Reborn"));

        if (Directory.Exists(this.symlinkGameConfigs.FullName) && (!Directory.Exists(this.GameConfigRoot.FullName)))
        {
            // Copy directory, as moving it may break other mappings to the location
            this.GameConfigRoot.Create();
            File.Copy(this.symlinkGameConfigs.FullName + "/FFXIV.cfg", this.GameConfigRoot + "/FFXIV.cfg");
            File.Copy(this.symlinkGameConfigs.FullName + "/FFXIV_BOOT.cfg", this.GameConfigRoot + "/FFXIV_BOOT.cfg");
        }
        else
        {
            this.GameConfigRoot.Create();
        }
    }

    public FileInfo GetXLConfig(string fileName)
    {
        return new FileInfo(Path.Combine(this.XLConfigRoot.FullName, fileName));
    }

    public FileInfo GetGameConfig(string fileName)
    {
        return new FileInfo(Path.Combine(this.GameConfigRoot.FullName, fileName));
    }

    public DirectoryInfo GetXLConfigFolder(string folderName = ".")
    {
        var folder = new DirectoryInfo(Path.Combine(this.XLConfigRoot.FullName, folderName));

        if (!folder.Exists)
            folder.Create();

        return folder;
    }

    public DirectoryInfo GetGameConfigFolder(string folderName = ".")
    {
        var folder = new DirectoryInfo(Path.Combine(this.GameConfigRoot.FullName, folderName));

        if (!folder.Exists)
            folder.Create();

        return folder;
    }
}