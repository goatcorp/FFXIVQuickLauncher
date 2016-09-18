using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace XIVLauncher
{
    static class XIVGame
    {

        public static void LaunchGame(string realsid, int language, bool dx11)
        {
            Process ffxivgame = new Process();
            if (dx11) { ffxivgame.StartInfo.FileName = Settings.GetGamePath() + "/game/ffxiv_dx11.exe"; } else { ffxivgame.StartInfo.FileName = Settings.GetGamePath() + "/game/ffxiv.exe"; }
            ffxivgame.StartInfo.Arguments = "DEV.TestSID=" + realsid + " language=" + language;
            ffxivgame.Start();
        }

        private static string GetSTORED() //this is needed to be able to access the login site correctly
        {
            WebClient LoginInfo = new WebClient();
            LoginInfo.Headers.Add("user-agent", "SQEXAuthor/2.0.0(Windows 6.2; ja-jp; 15c5fd77b2)");
            string reply = LoginInfo.DownloadString("https://ffxiv-login.square-enix.com/oauth/ffxivarr/login/top?lng=en&rgn=3");

            
            Regex storedre = new Regex(@"value=""(.*)""");

            var test = storedre.Matches(reply);
            return test[0].Value.Substring(7,754);
        }

        private static string GetSID(string username, string password, string otp)
        {

            using (WebClient LoginData = new WebClient())
            {
                LoginData.Headers.Add("user-agent", "SQEXAuthor/2.0.0(Windows 6.2; ja-jp; 15c5fd77b2)");
                LoginData.Headers.Add("Referer", "https://ffxiv-login.square-enix.com/oauth/ffxivarr/login/top?lng=en&rgn=3");
                LoginData.Headers.Add("Content-Type", "application/x-www-form-urlencoded");

                byte[] response =
                LoginData.UploadValues("https://ffxiv-login.square-enix.com/oauth/ffxivarr/login/login.send", new NameValueCollection() //get the session id with user credentials
                {
                    { "_STORED_", GetSTORED() },
                    { "sqexid", username },
                    { "password", password },
                    { "otppw", otp }
                });

                string reply = System.Text.Encoding.UTF8.GetString(response);

                Regex sidre = new Regex(@"sid,(.+?),");

                var test = sidre.Matches(reply);
                return test[0].Value.Substring(4, 56);
            }
        }

        
        public static string GetRealSID(string username, string password, string otp)
        {
            string hashstr = "ffxivboot.exe/" + GenerateHash(Settings.GetGamePath() + "/boot/ffxivboot.exe") + ",ffxivlauncher.exe/" + GenerateHash(Settings.GetGamePath() + "/boot/ffxivlauncher.exe") + ",ffxivupdater.exe/" + GenerateHash(Settings.GetGamePath() + "/boot/ffxivupdater.exe"); //make the string of hashed files to prove game version

            WebClient SidClient = new WebClient();
            SidClient.Headers.Add("X-Hash-Check", "enabled");
            SidClient.Headers.Add("user-agent", "SQEXAuthor/2.0.0(Windows 6.2; ja-jp; 15c5fd77b2)");
            SidClient.Headers.Add("Referer", "https://ffxiv-login.square-enix.com/oauth/ffxivarr/login/top?lng=en&rgn=3");
            SidClient.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
       
            InitiateSSLTrust();

            SidClient.UploadString("https://patch-gamever.ffxiv.com/http/win32/ffxivneo_release_game/" + GetLocalGamever() + "/" + GetSID(username,password,otp), hashstr); //request real session id

            return SidClient.ResponseHeaders["X-Patch-Unique-Id"];
        }
        

        private static string GetLocalGamever()
        {
            try
            {
                using (StreamReader sr = new StreamReader(Settings.GetGamePath()+@"/game/ffxivgame.ver"))
                {
                    string line = sr.ReadToEnd();
                    return line;
                }
            }
            catch (Exception e)
            {
                return "0";
            }
        }

        private static string GenerateHash(string file) //thanks SO
        {
            byte[] filebytes = File.ReadAllBytes(file);

            var hash = (new SHA1Managed()).ComputeHash(filebytes);
            string hashstring = string.Join("", hash.Select(b => b.ToString("x2")).ToArray());

            long length = new System.IO.FileInfo(file).Length;

            return length + "/" + hashstring;
        }

        private static void InitiateSSLTrust()
        {
            try
            {
                //Change SSL checks so that all checks pass, squares gamever server does strange things
                ServicePointManager.ServerCertificateValidationCallback =
                   new RemoteCertificateValidationCallback(
                        delegate
                        { return true; }
                    );
            }
            catch (Exception ex)
            {
                
            }
        }
    }
}
