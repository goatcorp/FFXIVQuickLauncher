using System;
using System.Threading;

namespace XIVLauncher.Common.Http
{
    public class OtpListener
    {
        private volatile HttpServer server;

        private const int HTTP_PORT = 4646;

        public event LoginEvent OnOtpReceived;

        public delegate void LoginEvent(string onetimePassword);

        private readonly Thread serverThread;

        public OtpListener(string version)
        {
            this.server = new HttpServer(HTTP_PORT, version);
            this.server.GetReceived += this.GetReceived;

            this.serverThread = new Thread(this.server.Start) { Name = "OtpListenerServerThread", IsBackground = true };
        }

        private void GetReceived(object sender, HttpServer.HttpServerGetEvent e)
        {
            if (e.Path.StartsWith("/ffxivlauncher/", StringComparison.Ordinal))
            {
                var otp = e.Path.Substring(15);

                OnOtpReceived?.Invoke(otp);
            }
        }

        public void Start()
        {
            this.serverThread.Start();
        }

        public void Stop()
        {
            this.server?.Stop();
        }
    }
}