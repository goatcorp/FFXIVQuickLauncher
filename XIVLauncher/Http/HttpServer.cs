using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace XIVLauncher.Http
{
    // This is a very dumb HTTP server that just accepts GETs and fires events with the requested URL
    public class HttpServer
    {
        private TcpListener _listener;
        public bool IsRunning { get; private set; }

        public EventHandler<HttpServerGetEvent> GetReceived;

        public class HttpServerGetEvent
        {
            public string Path { get; set; }
        }

        public HttpServer(int port)
        {
            _listener = new TcpListener(IPAddress.Any, port);
        }

        public void Start()
        {
            IsRunning = true;
            _listener.Start();

            while (IsRunning)
            {
                var client = _listener.AcceptTcpClient();

                var networkStream = client.GetStream();

                while (client.Connected)
                {
                    var message = new byte[1024];
                    networkStream.Read(message, 0, message.Length);

                    var messageString = Encoding.Default.GetString(message);
                    Debug.WriteLine(Encoding.Default.GetString(message));

                    GetReceived?.Invoke(this, new HttpServerGetEvent
                    {
                        Path = Regex.Match(messageString, "GET (?<url>.+) HTTP").Groups["url"].Value
                    });
                }

                client.Close();
            }
        }

        public void Stop()
        {
            IsRunning = false;
            _listener.Stop();
        }
    }

}