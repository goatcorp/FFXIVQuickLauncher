namespace XIVLauncher.Http
{
    internal class OtpListener
    {
        private volatile HttpServer _server;

        private const int HTTP_PORT = 4646;

        public LoginEvent OnOtpReceived;

        public delegate void LoginEvent(string onetimePassword);

        public OtpListener()
        {
            _server = new HttpServer(HTTP_PORT);
            _server.GetReceived += GetReceived;
        }

        private void GetReceived(object sender, HttpServer.HttpServerGetEvent e)
        {
            if (e.Path.StartsWith("/ffxivlauncher/"))
            {
                var otp = e.Path.Substring(15);

                OnOtpReceived?.Invoke(otp);
            }
        }

        public void Start()
        {
            _server.Start();
        }

        public void Stop()
        {
            if (_server != null && _server.IsRunning) _server.Stop();
        }
    }
}