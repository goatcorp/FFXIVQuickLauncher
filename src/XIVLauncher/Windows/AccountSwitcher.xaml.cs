using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using IWshRuntimeLibrary;
using XIVLauncher.Accounts;
using XIVLauncher.Common;
using XIVLauncher.Windows.ViewModel;

namespace XIVLauncher.Windows
{
    /// <summary>
    /// Interaction logic for AccountSwitcher.xaml
    /// </summary>
    public partial class AccountSwitcher : Window
    {
        private readonly AccountManager _accountManager;

        public EventHandler<XivAccount> OnAccountSwitchedEventHandler;

        public AccountSwitcher(AccountManager accountManager)
        {
            InitializeComponent();

            DataContext = new AccountSwitcherViewModel();

            _accountManager = accountManager;

            RefreshEntries();
        }

        private void RefreshEntries()
        {
            var accountEntries = new List<AccountSwitcherEntry>();

            foreach (var accountManagerAccount in _accountManager.Accounts)
            {
                var entry = new AccountSwitcherEntry
                {
                    Account = accountManagerAccount
                };

                Task.Run(() =>
                {
                    if (string.IsNullOrEmpty(accountManagerAccount.ThumbnailUrl))
                        accountManagerAccount.ThumbnailUrl = accountManagerAccount.FindCharacterThumb();

                    entry.UpdateProfileImage();
                });

                accountEntries.Add(entry);
            }

            _accountManager.Save();

            AccountListView.ItemsSource = accountEntries;
        }

        private bool _closing = false;

        private void AccountListView_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
                return;

            var selectedEntry = AccountListView.SelectedItem as AccountSwitcherEntry;

            OnAccountSwitchedEventHandler?.Invoke(this, selectedEntry.Account);

            _closing = true;
            Close();
        }

        private void AccountListViewContext_Opened(object sender, RoutedEventArgs e)
        {
            var selectedEntry = AccountListView.SelectedItem as AccountSwitcherEntry;
            AccountEntrySavePasswordCheck.IsChecked = !selectedEntry.Account.SavePassword;
        }

        private void AccountSwitcher_OnDeactivated(object sender, EventArgs e)
        {
            if (!_closing)
                Close();
        }

        private Bitmap BitmapImage2Bitmap(BitmapImage bitmapImage)
        {
            using(MemoryStream outStream = new MemoryStream())
            {
                BitmapEncoder enc = new BmpBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bitmapImage));
                enc.Save(outStream);
                System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(outStream);

                return new Bitmap(bitmap);
            }
        }

        // https://stackoverflow.com/questions/11434673/bitmap-save-to-save-an-icon-actually-saves-a-png
        void SaveAsIcon(Bitmap sourceBitmap, string filePath)
        {
            FileStream fs = new FileStream(filePath, FileMode.Create);
            // ICO header
            fs.WriteByte(0); fs.WriteByte(0);
            fs.WriteByte(1); fs.WriteByte(0);
            fs.WriteByte(1); fs.WriteByte(0);

            // Image size
            fs.WriteByte((byte)sourceBitmap.Width);
            fs.WriteByte((byte)sourceBitmap.Height);
            // Palette
            fs.WriteByte(0);
            // Reserved
            fs.WriteByte(0);
            // Number of color planes
            fs.WriteByte(0); fs.WriteByte(0);
            // Bits per pixel
            fs.WriteByte(32); fs.WriteByte(0);

            // Data size, will be written after the data
            fs.WriteByte(0);
            fs.WriteByte(0);
            fs.WriteByte(0);
            fs.WriteByte(0);

            // Offset to image data, fixed at 22
            fs.WriteByte(22);
            fs.WriteByte(0);
            fs.WriteByte(0);
            fs.WriteByte(0);

            // Writing actual data
            sourceBitmap.Save(fs, ImageFormat.Png);

            // Getting data length (file length minus header)
            long len = fs.Length - 22;

            // Write it in the correct place
            fs.Seek(14, SeekOrigin.Begin);
            fs.WriteByte((byte)len);
            fs.WriteByte((byte)(len >> 8));

            fs.Close();
        }

        private void CreateDesktopShortcut_OnClick(object sender, RoutedEventArgs e)
        {
            if (!(AccountListView.SelectedItem is AccountSwitcherEntry selectedEntry))
                return;

            var thumbnailPath = System.Reflection.Assembly.GetEntryAssembly().Location;

            if (!string.IsNullOrEmpty(selectedEntry.Account.ThumbnailUrl))
            {
                var thumbnailDirectory = Path.Combine(Paths.RoamingPath, "profileIcons");
                Directory.CreateDirectory(thumbnailDirectory);

                thumbnailPath = Path.Combine(thumbnailDirectory, $"{selectedEntry.Account.Id}.ico");

                SaveAsIcon(BitmapImage2Bitmap((BitmapImage) selectedEntry.ProfileImage), thumbnailPath);
            }

            var shDesktop = (object)"Desktop";

            var shell = new WshShell();
            var shortcutAddress = (string)shell.SpecialFolders.Item(ref shDesktop) + $@"\XIVLauncher - {selectedEntry.Account.UserName} {(selectedEntry.Account.UseSteamServiceAccount ? "(Steam)" : "")}.lnk";
            var shortcut = (IWshShortcut)shell.CreateShortcut(shortcutAddress);
            shortcut.Description = $"Open XIVLauncher with the {selectedEntry.Account.UserName} Square Enix account.";
            shortcut.TargetPath = Path.Combine(new DirectoryInfo(Environment.CurrentDirectory).Parent.FullName, "XIVLauncher.exe");
            shortcut.Arguments = $"--account={selectedEntry.Account.Id}";
            shortcut.WorkingDirectory = Environment.CurrentDirectory;
            shortcut.IconLocation = thumbnailPath;
            shortcut.Save();
        }

        private void RemoveAccount_OnClick(object sender, RoutedEventArgs e)
        {
            if (!(AccountListView.SelectedItem is AccountSwitcherEntry selectedEntry))
                return;

            _accountManager.RemoveAccount(selectedEntry.Account);

            RefreshEntries();
        }

        private void SetProfilePicture_OnClick(object sender, RoutedEventArgs e)
        {
            if (!(AccountListView.SelectedItem is AccountSwitcherEntry selectedEntry))
                return;

            var inputDialog = new ProfilePictureInputWindow(selectedEntry.Account);
            inputDialog.ShowDialog();

            var account = _accountManager.Accounts.First(a => a.Id == selectedEntry.Account.Id);
            account.ChosenCharacterName = inputDialog.ResultName;
            account.ChosenCharacterWorld = inputDialog.ResultWorld;
            _accountManager.Save();

            RefreshEntries();
        }

        private void DontSavePassword_OnChecked(object sender, RoutedEventArgs e)
        {
            if (!(AccountListView.SelectedItem is AccountSwitcherEntry selectedEntry))
                return;

            var account = _accountManager.Accounts.First(a => a.Id == selectedEntry.Account.Id);
            account.SavePassword = false;
            account.Password = string.Empty;
            _accountManager.Save();
        }

        private void DontSavePassword_OnUnchecked(object sender, RoutedEventArgs e)
        {
            if (!(AccountListView.SelectedItem is AccountSwitcherEntry selectedEntry))
                return;

            var account = _accountManager.Accounts.First(a => a.Id == selectedEntry.Account.Id);
            account.SavePassword = true;
            _accountManager.Save();
        }

        private void ModifyOtpUri_Click(object sender, RoutedEventArgs e)
        {
            if (!(AccountListView.SelectedItem is AccountSwitcherEntry selectedEntry))
                return;

            var otpDialog = new OtpUriSetupWindow(selectedEntry.Account.OtpUri);
            otpDialog.ShowDialog();

            var account = _accountManager.Accounts.First(a => a.Id == selectedEntry.Account.Id);
            account.OtpUri = otpDialog.Result;
            _accountManager.Save();
        }
    }
}