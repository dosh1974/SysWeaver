using System;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Net;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;

// https://github.com/SimpleStack/simplestack.orm



namespace SysWeaver
{
    /// <summary>
    /// Helpers to create and share http clients
    /// </summary>
    public static class WebTools
    {
        /// <summary>
        /// User agent to use for HttpClient's
        /// </summary>
        public static readonly ProductInfoHeaderValue UserAgent = ProductInfoHeaderValue.Parse("Anonymous");


        static readonly HttpClientHandler DefHandlerAutoDecomp = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All

        };

        static readonly HttpClientHandler NoCertDefHandlerAutoDecomp = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            ServerCertificateCustomValidationCallback = (requestMessage, certificate, chain, sslErrors) => true
        };


        static readonly HttpClientHandler DefHandler = new HttpClientHandler
        {

        };

        static readonly HttpClientHandler NoCertDefHandler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (requestMessage, certificate, chain, sslErrors) => true
        };

        /// <summary>
        /// Create a new http client with a user agent (<see cref="UserAgent"/>) and (optionally) automatic decompression
        /// </summary>
        /// <param name="useTor">If true, the client will proxy through the tor network (must be available, see <see cref="TorService.IsAvailable"/>)</param>
        /// <param name="ignoreCertErrors">If true, the client will ignore any certificate errors (all certificates is ok - very dangerous). Ignored if <paramref name="useTor"/> is true.</param>
        /// <param name="autoDecompress">If true, the client will automatically decompress any compressed response data</param>
        /// <returns>A new http client, the caller owns it and may dispose it</returns>
        /// <exception cref="InvalidOperationException">If <paramref name="useTor"/> is true and SysWeaver.Tor isn't available</exception>
        /// <remarks>The http message handlers (except for tor) are shared by all clients and are never disposed (disposing the client doesn't dispose the handler)</remarks>
        public static HttpClient CreateHttpClient(bool useTor = false, bool ignoreCertErrors = false, bool autoDecompress = true)
        {
            HttpClient client;
            if (useTor)
            {
                if (!TorService.IsAvailable)
                    throw new InvalidOperationException("SysWeaver.Tor is not found! Can't use Tor!");
                client = TorService.CreateTorClient(autoDecompress);
            } else
            {
                client = new HttpClient(
                    autoDecompress
                    ?
                        (ignoreCertErrors ? NoCertDefHandlerAutoDecomp : DefHandlerAutoDecomp)
                    :
                        (ignoreCertErrors ? NoCertDefHandler : DefHandler)
                    , disposeHandler: false); // The handlers are static and shared by all clients, never dispose them
            }
            client.DefaultRequestHeaders.UserAgent.Add(UserAgent);
            return client;
        }

        /// <summary>
        /// The cached clients, the key is (timeOutInSeconds &lt;&lt; 1) | useTor
        /// </summary>
        static readonly ConcurrentDictionary<long, HttpClient> HttpClientCache = new();

        /// <summary>
        /// The maximum timeout of a http client in seconds (the timeout of a HttpClient can't exceed Int32.MaxValue milliseconds)
        /// </summary>
        const int MaxTimeOutInSeconds = int.MaxValue / 1000;

        /// <summary>
        /// Get a shared http client with a specific timeout (one instance is created and cached per timeout and tor option, with automatic decompression).
        /// Do NOT dispose!
        /// Do NOT modify  the state of the client!
        /// </summary>
        /// <param name="timeOutInSeconds">The request time out in seconds, 1 to 2147483</param>
        /// <param name="useTor">If true, the client will proxy through the tor network (must be available, see <see cref="TorService.IsAvailable"/>)</param>
        /// <returns>A shared http client</returns>
        /// <exception cref="ArgumentOutOfRangeException">If <paramref name="timeOutInSeconds"/> is less than 1 or greater than 2147483 (Int32.MaxValue milliseconds)</exception>
        /// <exception cref="InvalidOperationException">If <paramref name="useTor"/> is true and SysWeaver.Tor isn't available</exception>
        /// <remarks>Thread safe, no allocations are made once the client is created</remarks>
        public static HttpClient GetHttpClient(int timeOutInSeconds, bool useTor = false)
        {
            var c = HttpClientCache;
            var key = ((long)timeOutInSeconds << 1) | (useTor ? 1L : 0L);
            if (c.TryGetValue(key, out var h))
                return h;
            ArgumentOutOfRangeException.ThrowIfLessThan(timeOutInSeconds, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(timeOutInSeconds, MaxTimeOutInSeconds);
            lock (c)
            {
                if (c.TryGetValue(key, out h))
                    return h;
                h = CreateHttpClient(useTor);
                h.Timeout = TimeSpan.FromSeconds(timeOutInSeconds);
                c[key] = h;
                return h;
            }
        }



        /// <summary>
        /// A shared http client that you can use (automatic decompression, no tor, certificates are validated, the default timeout of 100 seconds).
        /// Do NOT dispose!
        /// Do NOT modify the state of the client!
        /// </summary>
        public static HttpClient HttpClient => InternalHttpClients[1].Value;

        /// <summary>
        /// Get shared http client that you can use (one instance is created and cached per combination of options, with the default timeout of 100 seconds).
        /// Do NOT dispose!
        /// Do NOT modify the state of the client!
        /// </summary>
        /// <param name="useTor">If true, the client will proxy through the tor network (must be available, see <see cref="TorService.IsAvailable"/>)</param>
        /// <param name="ignoreCertErrors">If true, the client will ignore any certificate errors (all certificates is ok - very dangerous)</param>
        /// <param name="autoDecompress">If true, the client will automatically decompress any compressed response data</param>
        /// <returns>A shared http client</returns>
        /// <exception cref="InvalidOperationException">If <paramref name="useTor"/> is true and SysWeaver.Tor isn't available</exception>
        /// <remarks>Thread safe</remarks>
        public static HttpClient GetSharedHttpClient(bool useTor = false, bool ignoreCertErrors = false, bool autoDecompress = true)
            => InternalHttpClients[
                (autoDecompress ? 1 : 0) |
                (ignoreCertErrors ? 2 : 0) |
                (useTor ? 4 : 0)
                ].Value;

        static readonly Lazy<HttpClient>[] InternalHttpClients =
        [
            new Lazy<HttpClient>(() => CreateHttpClient(false, false, false)),
            new Lazy<HttpClient>(() => CreateHttpClient(false, false, true)),
            new Lazy<HttpClient>(() => CreateHttpClient(false, true, false)),
            new Lazy<HttpClient>(() => CreateHttpClient(false, true, true)),
            new Lazy<HttpClient>(() => CreateHttpClient(true, false, false)),
            new Lazy<HttpClient>(() => CreateHttpClient(true, false, true)),
            new Lazy<HttpClient>(() => CreateHttpClient(true, true, false)),
            new Lazy<HttpClient>(() => CreateHttpClient(true, true, true)),
        ];



        







    }




}
