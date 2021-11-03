using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace XIVLauncher.Http
{
    // This is a very dumb HTTP server that just accepts GETs and fires events with the requested URL
    public class HttpServer
    {
        private TcpListener _listener;
        private readonly byte[] httpResponse = Encoding.Default.GetBytes(
            "HTTP/1.0 200 OK\n"+
            "Content-Type: application/json; charset=UTF-8\n"+
            "\n"+
            "{app:\"XIVLauncher\", version: \"" +
#if !XL_NOAUTOUPDATE
            Util.GetAssemblyVersion() +
#else
            Util.GetGitHash() +
#endif
            "\"}"
        );

        public EventHandler<HttpServerGetEvent> GetReceived;

        private bool _isRunning = false;

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
            try
            {
                _listener.Start();
                _isRunning = true;

                while (_isRunning)
                {
                    if (!_listener.Pending())
                    {
                        Thread.Sleep(200);
                        continue;
                    }

                    var client = _listener.AcceptTcpClient();

                    while (client.Connected)
                    {
                        var networkStream = client.GetStream();

                        var message = new byte[1024];
                        networkStream.Read(message, 0, message.Length);

                        var messageString = Encoding.Default.GetString(message);
                        Debug.WriteLine(Encoding.Default.GetString(message));

                        networkStream.Write(httpResponse, 0, httpResponse.Length);

                        networkStream.Close(3);

                        GetReceived?.Invoke(this, new HttpServerGetEvent
                        {
                            Path = Regex.Match(messageString, "GET (?<url>.+) HTTP").Groups["url"].Value
                        });
                    }

                    client.Close();
                }
            }
            catch
            {
                // ignored
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _listener.Stop();
        }
    }

}
