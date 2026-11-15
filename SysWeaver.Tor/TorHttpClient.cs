using Knapcode.TorSharp;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;


namespace SysWeaver.Tor
{
    /// <summary>
    /// An <see cref="HttpClient"/> that makes its requests through the Tor network (Tor is downloaded and started when the first client is created)
    /// </summary>
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

        /// <summary>
        /// The (socks5) proxy that goes through Tor
        /// </summary>
        public static readonly WebProxy Proxy = new WebProxy(new Uri("socks5://localhost:" + Settings.TorSettings.SocksPort));

        /// <summary>
        /// Create a client that uses Tor
        /// </summary>
        /// <param name="autoDecompress">True to automatically decompress responses</param>
        /// <returns>The client (dispose it when done)</returns>
        public static HttpClient Create(bool autoDecompress = true)
        {
            Init();
            var handler = new HttpClientHandler
            {
                Proxy = Proxy,
                AutomaticDecompression = autoDecompress ? (DecompressionMethods.All) : 0
            };
            return new TorHttpClient(handler);
        }

        TorHttpClient(HttpClientHandler handler) : base(handler)
        {
            Handler = handler;
        }

        readonly HttpClientHandler Handler;

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            Handler.Dispose();
        }


    }

}
