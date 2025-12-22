using System.Net;
using System.Net.Http;

namespace XIVLauncher.Common.Http.HappyEyeballs;

public class HappyHttpClient : HttpClient
{
    // FIXME: This really needs to be DI, but I'm too lazy to build that out at the moment.
    public static HappyEyeballsCallback SharedCallback { get; } = new();
    public static HappyHttpClient SharedClient { get; } = new(SharedCallback);

    public HappyHttpClient(HappyEyeballsCallback callback)
        : base(new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            ConnectCallback = callback.ConnectCallback,
        })
    {
    }

    public HappyHttpClient()
        : this(SharedCallback)
    {
    }
}
