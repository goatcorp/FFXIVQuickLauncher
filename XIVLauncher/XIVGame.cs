using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace XIVLauncher
{
    static class XIVGame
    {
        /// <summary>
        /// Launches FFXIV with the supplied parameters.
        /// </summary>
        /// <param name="realsid">Real SessionID</param>
        /// <param name="language">language(0=japanese,1=english,2=french,3=german)</param>
        /// <param name="dx11">Runs the game in dx11 mode if true</param>
        /// <param name="expansionlevel">current level of expansions loaded(0=ARR/default,1=Heavensward)</param>
        public static void LaunchGame(string realsid, int language, bool dx11, int expansionlevel)
        {
            try {
                Process ffxivgame = new Process();
                if (dx11) { ffxivgame.StartInfo.FileName = Settings.GetGamePath() + "/game/ffxiv_dx11.exe"; } else { ffxivgame.StartInfo.FileName = Settings.GetGamePath() + "/game/ffxiv.exe"; }
                ffxivgame.StartInfo.Arguments = $"DEV.TestSID={realsid} DEV.MaxEntitledExpansionID={expansionlevel} language={language}";
                ffxivgame.Start();
            }catch(Exception exc)
            {
                MessageBox.Show("Could not launch executable. Is your game path correct?\n\n" + exc, "Launch failed", MessageBoxButtons.OK);
            }
        }

        /// <summary>
        /// Gets a real SessionID for the supplied credentials.
        /// </summary>
        /// <param name="username">Sqare Enix ID</param>
        /// <param name="password">Password</param>
        /// <param name="otp">OTP</param>
        /// <returns></returns>
        public static string GetRealSid(string username, string password, string otp)
        {
            string hashstr = "";
            try
            {
                hashstr = "ffxivboot.exe/" + GenerateHash(Settings.GetGamePath() + "/boot/ffxivboot.exe") + ",ffxivlauncher.exe/" + GenerateHash(Settings.GetGamePath() + "/boot/ffxivlauncher.exe") + ",ffxivupdater.exe/" + GenerateHash(Settings.GetGamePath() + "/boot/ffxivupdater.exe"); //make the string of hashed files to prove game version
            }
            catch (Exception exc)
            {
                MessageBox.Show("Could not generate hashes. Is your game path correct?\n\n" + exc, "Launch failed", MessageBoxButtons.OK);
            }

            WebClient sidClient = new WebClient();
            sidClient.Headers.Add("X-Hash-Check", "enabled");
            sidClient.Headers.Add("user-agent", "SQEXAuthor/2.0.0(Windows 6.2; ja-jp; 9e75ab3012)");
            sidClient.Headers.Add("Referer", "https://ffxiv-login.square-enix.com/oauth/ffxivarr/login/top?lng=en&rgn=3");
            sidClient.Headers.Add("Content-Type", "application/x-www-form-urlencoded");

            InitiateSslTrust();

            sidClient.UploadString("https://patch-gamever.ffxiv.com/http/win32/ffxivneo_release_game/" + GetLocalGamever() + "/" + GetSid(username, password, otp), hashstr); //request real session id

            return sidClient.ResponseHeaders["X-Patch-Unique-Id"];
        }


        private static string GetStored() //this is needed to be able to access the login site correctly
        {
            WebClient loginInfo = new WebClient();
            loginInfo.Headers.Add("user-agent", "SQEXAuthor/2.0.0(Windows 6.2; ja-jp; 9e75ab3012)");
            string reply = loginInfo.DownloadString("https://ffxiv-login.square-enix.com/oauth/ffxivarr/login/top?lng=en&rgn=3&isft=0&issteam=0");

            Regex storedre = new Regex(@"\t<\s*input .* name=""_STORED_"" value=""(?<stored>.*)"">");

            return storedre.Matches(reply)[0].Groups["stored"].Value;
        }

        public static string GetSid(string username, string password, string otp)
        {
            using (WebClient loginData = new WebClient())
            {
                loginData.Headers.Add("user-agent", "SQEXAuthor/2.0.0(Windows 6.2; ja-jp; 9e75ab3012)");
                loginData.Headers.Add("Referer", "https://ffxiv-login.square-enix.com/oauth/ffxivarr/login/top?lng=en&rgn=3&isft=0&issteam=0");
                loginData.Headers.Add("Content-Type", "application/x-www-form-urlencoded");

                byte[] response =
                loginData.UploadValues("https://ffxiv-login.square-enix.com/oauth/ffxivarr/login/login.send", new NameValueCollection() //get the session id with user credentials
                {
                    { "_STORED_", GetStored() },
                    { "sqexid", username },
                    { "password", password },
                    { "otppw", otp }
                });

                string reply = System.Text.Encoding.UTF8.GetString(response);

                Regex sidre = new Regex(@"sid,(?<sid>.*),terms");
                return sidre.Matches(reply)[0].Groups["sid"].Value;
            }
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

        private static string GenerateHash(string file)
        {
            byte[] filebytes = File.ReadAllBytes(file);

            var hash = (new SHA1Managed()).ComputeHash(filebytes);
            string hashstring = string.Join("", hash.Select(b => b.ToString("x2")).ToArray());

            long length = new System.IO.FileInfo(file).Length;

            return length + "/" + hashstring;
        }

        public static bool GetGateStatus()
        {
            try
            {
                using (WebClient client = new WebClient())
                {
                    string reply = client.DownloadString("http://frontier.ffxiv.com/worldStatus/gate_status.json");

                    return Convert.ToBoolean(int.Parse(reply[10].ToString()));
                }
            }
            catch (Exception exc)
            {
                MessageBox.Show("Failed getting gate status.\n\n" + exc, "Launch failed", MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return false;
            }

        }

        private static void InitiateSslTrust()
        {
            //Change SSL checks so that all checks pass, squares gamever server does strange things
            ServicePointManager.ServerCertificateValidationCallback =
                new RemoteCertificateValidationCallback(
                    delegate
                    { return true; }
                );
        }
    }
}
