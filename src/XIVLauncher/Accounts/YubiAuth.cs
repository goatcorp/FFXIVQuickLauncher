using System;
using System.Collections.Generic;
using System.Linq;
using Serilog;
using Yubico.YubiKey;
using Yubico.YubiKey.Oath;
using Yubico.PlatformInterop;

namespace XIVLauncher.Accounts
{
    public class YubiAuth
    {
        private const string ISSUER = "XIVLauncher";
        private const string ACCOUNT_NAME = "FFXIV";
        private const CredentialType AUTH_TYPE = CredentialType.Totp;
        private const CredentialPeriod TIME_PERIOD = CredentialPeriod.Period30;
        private const byte NUM_DIGITS = 6;
        private static string username = "";

        private static IYubiKeyDevice _yubiKey;
        public YubiKeyDeviceListener DeviceListener;
        public YubiAuth()
        {
            FindYubiDevice();
            DeviceListener = YubiKeyDeviceListener.Instance;
        }

        public static void SetUsername(string name)
        {
            username = name;
        }
        public static string GetUsername()
        {
            return username;
        }
        public string GetAccountName()
        {
            return ACCOUNT_NAME + "-" + username;
        }

        //Generates generic credential
        public Credential BuildCredential()
        {
            var credentialTotp = new Credential
            {
                Issuer = ISSUER,
                AccountName = GetAccountName(),
                Type = AUTH_TYPE,
                Period = TIME_PERIOD,
                Digits = NUM_DIGITS,
            };
            return credentialTotp;
        }

        //Generates credential to be put onto user's YubiKey
        public Credential BuildCredential(string key, bool useTouch)
        {
            var credentialTotp = new Credential
            {
                Issuer = ISSUER,
                AccountName = GetAccountName(),
                Type = AUTH_TYPE,
                Period = TIME_PERIOD,
                Secret = key,
                Digits = NUM_DIGITS,
                RequiresTouch = useTouch,
            };
            return credentialTotp;
        }

        //Finds YubiKey(s) that are plugged into a USB port
        public void FindYubiDevice()
        {
            //Find YubiKey device
            try
            {
                if (_yubiKey == null)
                {
                    IEnumerable<IYubiKeyDevice> keys = YubiKeyDevice.FindByTransport(Transport.UsbSmartCard);
                    SetYubiDevice(keys.First());
                }
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
            {
                Log.Debug("No YubiKey device was detected");
                Log.Debug(ex.ToString());
            }

        }

        public IYubiKeyDevice GetYubiDevice()
        {
            return _yubiKey;
        }

        public void SetYubiDevice(IYubiKeyDevice yubiKeyDevice)
        {
            _yubiKey = yubiKeyDevice;
        }

        //Checks for existing credentials on user's YubiKey that match the XIVLauncher generic credential
        public bool CheckForCredential(OathSession session)
        {
            try
            {
                IList<Credential> creds = session.GetCredentials().Where(credential => credential.Issuer == ISSUER && credential.AccountName == GetAccountName()).ToList();
                if (creds != null && creds.Count != 0)
                {
                    return true;
                }
                return false;
            }
            catch (SCardException)
            {
                Log.Error("YubiKey was removed during GET operation.");
                return false;
            }
        }

        //Adds the user's credential onto their YubiKey device
        public void CreateEntry(Credential cred)
        {
            try
            {
                new OathSession(_yubiKey).AddCredential(cred);
                Log.Debug("Successfully created new credential " + cred.Name);
            }
            catch (SCardException)
            {
                Log.Error("YubiKey was removed during ADD operation");
            }

        }

    }


}
