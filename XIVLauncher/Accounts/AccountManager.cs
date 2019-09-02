using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace XIVLauncher.Accounts
{
    public class AccountManager
    {
        public ObservableCollection<XivAccount> Accounts;

        public XivAccount CurrentAccount =>
            Accounts.FirstOrDefault(a => a.Id == Properties.Settings.Default.CurrentAccount);

        public AccountManager()
        {
            if (string.IsNullOrEmpty(Properties.Settings.Default.Accounts))
            {
                Accounts = new ObservableCollection<XivAccount>();
                Save();
            }

            Accounts = JsonConvert.DeserializeObject<ObservableCollection<XivAccount>>(Properties.Settings.Default
                .Accounts);

            Accounts.CollectionChanged += Accounts_CollectionChanged;
        }

        private void Accounts_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            Save();
        }

        public void UpdatePassword(XivAccount account, string password)
        {
            var existingAccount = Accounts.FirstOrDefault(a => a.Id == account.Id);
            existingAccount.Password = password;
        }

        public void AddAccount(XivAccount account)
        {
            var existingAccount = Accounts.FirstOrDefault(a => a.Id == account.Id);

            if (existingAccount != null && existingAccount.Password != account.Password)
            {
                existingAccount.Password = account.Password;
                return;
            }

            if (existingAccount != null)
                return;

            Accounts.Add(account);
        }

        public void RemoveAccount(XivAccount account)
        {
            Accounts.Remove(account);
        }

        public void Save()
        {
            Properties.Settings.Default.Accounts = JsonConvert.SerializeObject(Accounts);
            Properties.Settings.Default.Save();
        }
    }
}
