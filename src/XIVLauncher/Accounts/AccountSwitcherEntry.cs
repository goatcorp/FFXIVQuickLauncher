using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using XIVLauncher.Common;

namespace XIVLauncher.Accounts
{
    class AccountSwitcherEntry
    {
        private static readonly ImageSource DefaultImage = new BitmapImage(new Uri("pack://application:,,,/Resources/defaultprofile.png", UriKind.Absolute));

        public XivAccount Account { get; set; }
        public ImageSource ProfileImage { get; set; } = DefaultImage;

        public void UpdateProfileImage()
        {
            if (string.IsNullOrEmpty(Account.ThumbnailUrl))
                return;

            var cacheFolder = Path.Combine(Paths.RoamingPath, "profilePictures");
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
            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = stream;
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.EndInit();
            bitmapImage.Freeze();

            ProfileImage = bitmapImage;
        }
    }
}