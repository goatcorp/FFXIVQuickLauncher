using System.Threading;
using System.Windows;

namespace XIVLauncher.Http
{
    internal class OtpListener
    {
        private volatile HttpServer _server;

        private const int HTTP_PORT = 4646;

        public LoginEvent OnOtpReceived;

        public delegate void LoginEvent(string onetimePassword);

        private Thread _serverThread;

        public OtpListener()
        {
            _server = new HttpServer(HTTP_PORT);
            _server.GetReceived += GetReceived;

            _serverThread = new Thread(_server.Start) {Name = "OtpListenerServerThread", IsBackground = true};
        }

        private void GetReceived(object sender, HttpServer.HttpServerGetEvent e)
        {
            if (e.Path.StartsWith("/ffxivlauncher/"))
            {
                var otp = e.Path.Substring(15);
                if (otp.Length < 6)
                {
                    MessageBox.Show("Received malformed OTP code, please check macro.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                OnOtpReceived?.Invoke(otp);
            }
        }

        public void Start()
        {
            _serverThread.Start();
        }

        public void Stop()
        {
            _server?.Stop();
        }
    }
}