using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Auth;
using SysWeaver.Compression;

namespace SysWeaver.Net
{
    public sealed class FileProxy : IHttpServerModule, IDisposable, IPerfMonitored
    {

        public String Name { get; init; }

        public String[] OnlyForPrefixes { get; init; }

        public FileProxy(FileProxyParams p)
        {
            var root = p.WebRoot;
            var sourceRoot = p.SourceRoot;
            if (String.IsNullOrEmpty(root))
                throw new Exception("Web root may not be empty!");
            if (String.IsNullOrEmpty(sourceRoot))
                throw new Exception("Source root may not be empty!");
            Name = String.Concat("FileProxy ", root, " => ", sourceRoot);
            WebRootLen = root.Length;
            SourceRoot = sourceRoot;
            PerfMon = new PerfMonitor(Name);
            if (!root.FastEquals("/"))
                OnlyForPrefixes = [root];
            if (p.GetUserPassword(out var user, out var password, false))
            {
                OwnClient = true;
                var c = WebTools.CreateHttpClient(p.UseTor, p.IgnoreCertErrors, false);
                Client = c;
                if (user.FastToLower().FastEquals("bearer"))
                {
                    c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", password);
                }
                else
                {
                    var byteArray = Encoding.ASCII.GetBytes(String.Join(":", user, password));
                    c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

                }
            }
            else
            {
                Client = WebTools.GetSharedHttpClient(p.UseTor, p.IgnoreCertErrors, false);
            }
            AsyncHandler = HandleAsync;
            Auth = Authorization.GetRequiredTokens(p.Auth);
        }

        readonly IReadOnlyList<String> Auth;

        public void Dispose()
        {
            if (OwnClient)
                Client.Dispose();
        }

        readonly bool OwnClient;
        readonly HttpClient Client;

        public override string ToString() => Name;

        readonly int WebRootLen;
        readonly String SourceRoot;

        public Func<HttpServerRequest, ValueTask<IHttpRequestHandler>> AsyncHandler { get; init; }

        public PerfMonitor PerfMon { get; init;  }

        sealed class CacheEntry
        {
            public long Expires;
            public readonly String[] ResHeader;
            public readonly Byte[] Data;
            public readonly String ETag;
            public readonly int Response;

            public CacheEntry(long expires, string[] resHeader, byte[] data, string eTag, int response)
            {
                Expires = expires;
                ResHeader = resHeader;
                Data = data;
                ETag = eTag;
                Response = response;
            }
        }

        static readonly Func<CacheEntry, DateTime> GetCacheExp = e => e == null ? DateTime.MinValue : new DateTime(Interlocked.Read(ref e.Expires), DateTimeKind.Utc);

        readonly MemCache<String, CacheEntry> GetCache = new (GetCacheExp, StringComparer.Ordinal);
        readonly MemCache<String, CacheEntry> HeadCache = new(GetCacheExp, StringComparer.Ordinal);

        async ValueTask<IHttpRequestHandler> HandleAsync(HttpServerRequest context)
        {
            using var __ = PerfMon.Track(nameof(HandleAsync));
            var u = context.LocalUrl;
            var req = SourceRoot + u.Substring(WebRootLen);
#if DEBUG
            String loc = String.Concat("Proxied from \"", req, '"');
#else//DEBUG
            const String loc = "Proxied";
#endif//DEBUG
            var p = context.QueryStringStart;
            if (p > 0)
                req = String.Concat(req, '?', context.Url.Substring(p));
            MemCache<String, CacheEntry> cache = null;
            switch (context.HttpMethod)
            {
                case HttpServerMethods.HEAD:
                    cache = HeadCache;
                    break;
                case HttpServerMethods.GET:
                    cache = GetCache;
                    break;
            }
            String cacheKey = cache == null ? null : String.Join('\n', req, context.AcceptEncoding);
            Byte[] data = null;
            String[] resHeaders = null;
            int response = -1;
            if (cache != null)
            {
                var reqInput = await ProxyTools.GetFromRequest(context).ConfigureAwait(false);
                var ce = await cache.GetOrUpdateValueAsync(cacheKey, async (_, current) =>
                {
                    using var ___ = PerfMon.Track(nameof(HandleAsync) + ".Internal");
                    ValueTuple<Byte[], String[], int> res;
                    res = await ProxyTools.ProxyRequest(Client, reqInput.Item1, req, reqInput.Item2, reqInput.Item3).ConfigureAwait(false);
                    data = res.Item1;
                    resHeaders = res.Item2;
                    response = res.Item3;
                    Console.WriteLine("APA: " + req + " [" + response + "]");
                    String etag = null;
                    int maxAge = 0;
                    foreach (var header in resHeaders)
                    {
                        if (header.FastStartsWith("Cache-Control:"))
                        {
                            var values = header.Substring(14).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                            foreach (var y in values)
                            {
                                if (y.FastStartsWith("max-age="))
                                {
                                    if (int.TryParse(y.Substring(8), out var x))
                                        if (x > maxAge)
                                            maxAge = x;
                                    break;
                                }

                            }
                            continue;
                        }
                        if (header.FastStartsWith("ETag:"))
                            etag = etag ?? header.Substring(5).Trim();
                    }
                    if (maxAge <= 0)
                        return null;
                    var et = DateTime.UtcNow.AddSeconds(maxAge).Ticks;
                    if (response == 304)
                    {
                        if (current != null)
                        {
                            Interlocked.Exchange(ref current.Expires, et);
                            return current;
                        }
                        return null;
                    }
                    return new CacheEntry(et, resHeaders, data, etag, response);
                }).ConfigureAwait(false);
                if (ce != null)
                {
                    if (ce.ETag != null) 
                    {
                        foreach (var header in reqInput.Item2)
                        {
                            if (header.FastStartsWith("If-None-Match:"))
                            {
                                if (ce.ETag.FastEquals(header.Substring(14).Trim()))
                                {
                                    context.SetResStatusCode(304);
                                    return HttpServerTools.AlreadyHandled;
                                }
                                break;
                            }
                        }
                    }
                    data = ce.Data;
                    resHeaders = ce.ResHeader;
                    response = ce.Response;
                }
            }
            if (response < 0)
            {
                var reqInput = await ProxyTools.GetFromRequest(context).ConfigureAwait(false);
                var res = await ProxyTools.ProxyRequest(Client, reqInput.Item1, req, reqInput.Item2, reqInput.Item3).ConfigureAwait(false);
                data = res.Item1;
                resHeaders = res.Item2;
                response = res.Item3;
            }
            await ProxyTools.SetToRequest(context, resHeaders, response, data).ConfigureAwait(false);
            return HttpServerTools.AlreadyHandled;

        }


    }






}
