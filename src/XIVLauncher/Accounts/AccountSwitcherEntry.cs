using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace XIVLauncher.Accounts
{
    class AccountSwitcherEntry
    {
        public XivAccount Account { get; set; }
        public ImageSource ProfileImage { get; set; } = new BitmapImage(new Uri("pack://application:,,,/Resources/defaultprofile.png", UriKind.Absolute));
        
        public void UpdateProfileImage()
        {
            if (string.IsNullOrEmpty(Account.ThumbnailUrl))
                return;

            using (var client = new WebClient())
            {
                var imageBytes = client.DownloadData(Account.ThumbnailUrl);

                using (var stream = new MemoryStream(imageBytes))
                {
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
    }
}
