using System.Net;

namespace XIVLauncher.Core.Accounts;

class AccountSwitcherEntry
{
    public AccountSwitcherEntry(XivAccount account)
    {
        this.Account = account;
    }

    public XivAccount Account { get; private set; }

    public TextureWrap? ProfileImage { get; set; }

    public void UpdateProfileImage(DirectoryInfo storage)
    {
        if (string.IsNullOrEmpty(Account.ThumbnailUrl))
            return;

        var cacheFolder = Path.Combine(storage.FullName, "profilePictures");
        Directory.CreateDirectory(cacheFolder);

        var uri = new Uri(Account.ThumbnailUrl);
        var cacheFile = Path.Combine(cacheFolder, uri.Segments.Last());

        byte[] imageBytes;

        if (File.Exists(cacheFile))
        {
            imageBytes = File.ReadAllBytes(cacheFile);
        }
        else
        {
            using (var client = new WebClient())
            {
                imageBytes = client.DownloadData(uri);
            }

            File.WriteAllBytes(cacheFile, imageBytes);
        }

        using var stream = new MemoryStream(imageBytes);

        // TODO
        /*
        var bitmapImage = new BitmapImage();
        bitmapImage.BeginInit();
        bitmapImage.StreamSource = stream;
        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapImage.EndInit();
        bitmapImage.Freeze();

        ProfileImage = bitmapImage;
        */
    }
}