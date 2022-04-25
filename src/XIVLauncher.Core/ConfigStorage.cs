namespace XIVLauncher.Core;

public class ConfigStorage
{
    public DirectoryInfo XLConfigRoot { get; }
    public DirectoryInfo GameConfigRoot { get; }

    private readonly DirectoryInfo legacyXLConfigRoot;
    private readonly DirectoryInfo legacyGameConfigRoot;
    private readonly DirectoryInfo symlinkGameConfigs;
    
    public ConfigStorage(string appName, string configFolder)
    {
        // https://developers.redhat.com/blog/2018/11/07/dotnet-special-folder-api-linux
        // Support for migrating old configs, to bring storage inline with Windows
        this.legacyXLConfigRoot = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), $".{appName}"));
        this.legacyGameConfigRoot = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), $".{appName}/ffxivConfig"));

        this.XLConfigRoot = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher"));
        this.GameConfigRoot = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher"));
        this.symlinkGameConfigs = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents/My Games/FINAL FANTASY XIV - A Realm Reborn"));

        if (Directory.Exists(this.symlinkGameConfigs.FullName) && (!Directory.Exists(this.GameConfigRoot.FullName)))
        {
            // Copy directory, as moving it may break other mappings to the location
            Directory.CreateDirectory(this.GameConfigRoot.FullName);
            File.Copy(this.symlinkGameConfigs.FullName + "/FFXIV.cfg", this.GameConfigRoot + "/FFXIV.cfg");
            File.Copy(this.symlinkGameConfigs.FullName + "/FFXIV_BOOT.cfg", this.GameConfigRoot + "/FFXIV_BOOT.cfg");
        }
        else
        {
            if (Directory.Exists(legacyGameConfigRoot.FullName) && (!Directory.Exists(this.GameConfigRoot.FullName)))
                Directory.Move(legacyXLConfigRoot.FullName, XLConfigRoot.FullName);
        }

        if (!this.GameConfigRoot.Exists)
        {
            this.GameConfigRoot.Create();
        }
    }

    public FileInfo GetXLConfig(string fileName)
    {
        if (File.Exists(this.legacyXLConfigRoot.FullName + $"/{fileName}"))
        {
            File.Move(this.legacyXLConfigRoot.FullName + $"/{fileName}", this.XLConfigRoot + $"/{fileName}");
        }

        return new FileInfo(Path.Combine(this.XLConfigRoot.FullName, fileName));
    }

    public FileInfo GetGameConfig(string fileName)
    {
        if (File.Exists(this.legacyGameConfigRoot.FullName + $"/{fileName}"))
        {
            File.Move(this.legacyGameConfigRoot.FullName + $"/{fileName}", this.GameConfigRoot + $"/{fileName}");
        }

        return new FileInfo(Path.Combine(this.GameConfigRoot.FullName, fileName));
    }

    public DirectoryInfo GetXLConfigFolder(string folderName)
    {
        var folder = new DirectoryInfo(Path.Combine(this.XLConfigRoot.FullName, folderName));

        if (!folder.Exists)
            folder.Create();

        return folder;
    }

    public DirectoryInfo GetGameConfigFolder(string folderName)
    {
        var folder = new DirectoryInfo(Path.Combine(this.GameConfigRoot.FullName, folderName));

        if (!folder.Exists)
            folder.Create();

        return folder;
    }
}