using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver.Net
{
    public sealed class ProxyRequestCache
    {


        /// <summary>
        /// Get some stats about the GET cache performance
        /// </summary>
        /// <param name="hitRatio">The ratio [0, 1] of cache hits (GetOrUpdate returns an existing item)</param>
        /// <param name="semiHitRatio">The ratio [0, 1] of semi cache hits (GetOrUpdate returns an existing item, but had to take a lock to get it, so less optimal)</param>
        /// <param name="missRatio">The ratio [0, 1] of cache misses (GetOrUpdate doesn't have an item, and a new one have to be created)</param>
        /// <param name="hitCount">Number of cache hits (GetOrUpdate returns an existing item)</param>
        /// <param name="semiHitCount">Number of semi cache hits (GetOrUpdate returns an existing item, but had to take a lock to get it, so less optimal)</param>
        /// <param name="missCount">Number of cache misses (GetOrUpdate doesn't have an item, and a new one have to be created)</param>
        /// <param name="size">Number of items in the cache</param>
        /// <returns>The total number of GetOrUpdate requests</returns>
        public long GetGetStats(out double hitRatio, out double semiHitRatio, out double missRatio,
            out long hitCount, out long semiHitCount, out long missCount, out long size)
            => GetCache.GetStats(out hitRatio, out semiHitRatio, out missRatio,
            out hitCount, out semiHitCount, out missCount, out size);

        /// <summary>
        /// Get some stats about the HEAD cache performance
        /// </summary>
        /// <param name="hitRatio">The ratio [0, 1] of cache hits (GetOrUpdate returns an existing item)</param>
        /// <param name="semiHitRatio">The ratio [0, 1] of semi cache hits (GetOrUpdate returns an existing item, but had to take a lock to get it, so less optimal)</param>
        /// <param name="missRatio">The ratio [0, 1] of cache misses (GetOrUpdate doesn't have an item, and a new one have to be created)</param>
        /// <param name="hitCount">Number of cache hits (GetOrUpdate returns an existing item)</param>
        /// <param name="semiHitCount"> (GetOrUpdate returns an existing item, but had to take a lock to get it, so less optimal)</param>
        /// <param name="missCount">Number of cache misses (GetOrUpdate doesn't have an item, and a new one have to be created)</param>
        /// <param name="size">Number of items in the cache</param>
        /// <returns>The total number of GetOrUpdate requests</returns>
        public long GetHeadStats(out double hitRatio, out double semiHitRatio, out double missRatio,
            out long hitCount, out long semiHitCount, out long missCount, out long size)
            => HeadCache.GetStats(out hitRatio, out semiHitRatio, out missRatio,
            out hitCount, out semiHitCount, out missCount, out size);

        /// <summary>
        /// Get some stats for the GET cache using Stats type
        /// </summary>
        /// <param name="system">A system name for the cache</param>
        /// <param name="prefix">An optional prefix to add to the stats name</param>
        /// <returns>Stats</returns>
        public IEnumerable<Stats> GetCacheStats(String system, String prefix = "") => GetCache.GetStats(system, prefix);

        /// <summary>
        /// Get some stats for the HEAD cache using Stats type
        /// </summary>
        /// <param name="system">A system name for the cache</param>
        /// <param name="prefix">An optional prefix to add to the stats name</param>
        /// <returns>Stats</returns>
        public IEnumerable<Stats> HeadCacheStats(String system, String prefix = "") => HeadCache.GetStats(system, prefix);

        /// <summary>
        /// A cache wrapper around a proxy request.
        /// Only GET and HEAD requests are cached as of now (POST caching would require a hash computation of the post data, even if the request isn't cached)
        /// </summary>
        /// <param name="context">The request</param>
        /// <param name="req">The request uri (this is what is used as the cache key)</param>
        /// <param name="doRequest">Function that performs a fresh request (as in not being cached)</param>
        /// <returns></returns>
        public async Task<IHttpRequestHandler> HandleAsync(HttpServerRequest context, String req, Func<String, ProxyData, Task<ProxyData>> doRequest)
        {
            FastMemCache<String, CacheEntry> cache = null;
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
            ProxyData reqInput = await ProxyTools.GetFromRequest(context).ConfigureAwait(false);
            ProxyData proxyRet = null;
            if (cache != null)
            {
                var ce = await cache.GetOrUpdateWithExistingAsync(cacheKey, async (_, current) =>
                {
                    proxyRet = await doRequest(req, reqInput).ConfigureAwait(false);
                    String etag = null;
                    int maxAge = 0;
                    foreach (var header in proxyRet.Headers)
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
                    if (proxyRet.StatusCode == 304)
                    {
                        if (current != null)
                        {
                            Interlocked.Exchange(ref current.Expires, et);
                            return current;
                        }
                        return null;
                    }
                    return new CacheEntry(et, etag, proxyRet);
                }).ConfigureAwait(false);
                if (ce != null)
                {
                    var etag = ce.ETag;
                    if ((etag != null) && etag.FastEquals(context.IfNoneMatch))
                    {
                        context.SetResStatusCode(304);
                        return HttpServerTools.AlreadyHandled;
                    }
                    proxyRet = ce.Data;
                }
            }
            if (proxyRet == null)
                proxyRet = await doRequest(req, reqInput).ConfigureAwait(false);
            await ProxyTools.SetToRequest(context, proxyRet).ConfigureAwait(false);
            return HttpServerTools.AlreadyHandled;
        }



        sealed class CacheEntry
        {
            public long Expires;
            public readonly String ETag;
            public readonly ProxyData Data;

            public CacheEntry(long expires, String etag, ProxyData data)
            {
                Expires = expires;
                Data = data;
                ETag = etag;
            }
        }

        static readonly Func<CacheEntry, DateTime> GetCacheExp = e => e == null ? DateTime.MinValue : new DateTime(Interlocked.Read(ref e.Expires), DateTimeKind.Utc);

        readonly FastMemCache<String, CacheEntry> GetCache = new(GetCacheExp, StringComparer.Ordinal);
        readonly FastMemCache<String, CacheEntry> HeadCache = new(GetCacheExp, StringComparer.Ordinal);

    }




}
