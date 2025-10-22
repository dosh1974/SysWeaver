using Knapcode.TorSharp;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;


namespace SysWeaver.Tor
{
    public class TorHttpClient : HttpClient
    {
        static volatile bool IsInited;
        static readonly Object Lock = new object();

        static readonly TorSharpSettings Settings = new TorSharpSettings
        {
            PrivoxySettings = { Disable = true }
        };

        static void Init()
        {
            if (IsInited)
                return;
            lock (Lock)
            {
                if (IsInited)
                    return;
                InitAsync().RunAsync();
                IsInited = true;
            }
        }

        static async Task InitAsync()
        {
            // download Tor
            using (var httpClient = new HttpClient())
            {
                var fetcher = new TorSharpToolFetcher(Settings, httpClient);
                await fetcher.FetchAsync().ConfigureAwait(false);
            }
            var proxy = new TorSharpProxy(Settings);
            await proxy.ConfigureAndStartAsync().ConfigureAwait(false);
            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                proxy.Stop();
                proxy.Dispose();
            };
        }

        public static readonly WebProxy Proxy = new WebProxy(new Uri("socks5://localhost:" + Settings.TorSettings.SocksPort));

        public static HttpClient Create()
        {
            Init();
            var handler = new HttpClientHandler
            {
                Proxy = Proxy,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
            };
            return new TorHttpClient(handler);
        }

        TorHttpClient(HttpClientHandler handler) : base(handler)
        {
            Handler = handler;
        }

        readonly HttpClientHandler Handler;

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            Handler.Dispose();
        }


    }

}
