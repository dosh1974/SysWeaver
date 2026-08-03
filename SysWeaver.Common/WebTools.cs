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
        /// Create a http client with a user agent and automatic decompression
        /// </summary>
        /// <param name="useTor">If true, the client will proxy through the tor network (must be available)</param>
        /// <param name="ignoreCertErrors">If true, the client will ignore any certificate errors (all certificates is ok - very dangerous)</param>
        /// <param name="autoDecompress">If true, the client will automatically decompress any compressed response data</param>
        /// <returns></returns>
        public static HttpClient CreateHttpClient(bool useTor = false, bool ignoreCertErrors = false, bool autoDecompress = true)
        {
            HttpClient client;
            if (useTor)
            {
                if (!TorService.IsAvailable)
                    throw new Exception("SysWeaver.Tor is not found! Can't use Tor!");
                client = TorService.CreateTorClient(autoDecompress);
            } else
            {
                client = new HttpClient(
                    autoDecompress
                    ?
                        (ignoreCertErrors ? NoCertDefHandlerAutoDecomp : DefHandlerAutoDecomp)
                    :
                        (ignoreCertErrors ? NoCertDefHandler : DefHandler)
                    );
            }
            client.DefaultRequestHeaders.UserAgent.Add(UserAgent);
            return client;
        }

        static readonly ConcurrentDictionary<String, HttpClient> HttpClientCache = new(StringComparer.Ordinal);

        /// <summary>
        /// Get a shared http client with a specific timeout.
        /// Do NOT dispose!
        /// Do NOT modify  the state of the client!
        /// </summary>
        /// <param name="timeOutInSeconds">The request time out in seconds</param>
        /// <param name="useTor">If true, the client will proxy through the tor network (must be available)</param>
        /// <returns>A http client</returns>
        public static HttpClient GetHttpClient(int timeOutInSeconds, bool useTor = false)
        {
            var c = HttpClientCache;
            var key = String.Join('_', timeOutInSeconds, useTor);
            if (c.TryGetValue(key, out var h))
                return h;
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
        /// A shared http client that you can use.
        /// Do NOT dispose!
        /// Do NOT modify the state of the client!
        /// </summary>
        public static HttpClient HttpClient => InternalHttpClients[1].Value;

        /// <summary>
        /// Get shared http client that you can use.
        /// Do NOT dispose!
        /// Do NOT modify the state of the client!
        /// </summary>
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
