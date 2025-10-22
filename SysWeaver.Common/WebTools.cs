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


        static readonly HttpClientHandler DefHandler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli

        };

        static readonly HttpClientHandler NoCertDefHandler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            ServerCertificateCustomValidationCallback = (requestMessage, certificate, chain, sslErrors) => true
        };

        /// <summary>
        /// Create a http client with a user agent and automatic decompression
        /// </summary>
        /// <param name="useTor">If true, the client will proxy through the tor network (must be available)</param>
        /// <param name="ignoreCertErrors">If true, the client will ignore any certificate errors (all certificates is ok - very dangerous)</param>
        /// <returns></returns>
        public static HttpClient CreateHttpClient(bool useTor = false, bool ignoreCertErrors = false)
        {
            HttpClient client;
            if (useTor)
            {
                if (!TorService.IsAvailable)
                    throw new Exception("SysWeaver.Tor is not found! Can't use Tor!");
                client = TorService.CreateTorClient();
            }else
            {
                client = new HttpClient(ignoreCertErrors ? NoCertDefHandler : DefHandler);
            }
            client.DefaultRequestHeaders.UserAgent.Add(UserAgent);
            return client;
        }

        static readonly ConcurrentDictionary<String, HttpClient> HttpClientCache = new (StringComparer.Ordinal);

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
        /// Do NOT modify  the state of the client!
        /// </summary>
        public static HttpClient HttpClient => InternalHttpClient.Value;

        static readonly Lazy<HttpClient> InternalHttpClient = new Lazy<HttpClient>(() => CreateHttpClient());




    }




}
