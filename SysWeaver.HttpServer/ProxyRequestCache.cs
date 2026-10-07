using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver.Net
{
    /// <summary>
    /// A response cache for proxied GET and HEAD requests (used by <see cref="FileProxy"/> and the reverse proxy).
    /// Responses are cached for the max-age of the upstream Cache-Control header (not cached if missing or zero, or if the response is "private" or "no-store"),
    /// keyed by the upstream url, the Accept-Encoding header and the forwarded Cookie and Authorization headers (so responses are only shared between requests with the same credentials).
    /// </summary>
    /// <remarks>
    /// Thread safe. Set-Cookie headers are never stored in the cache (only the request that caused the upstream request gets them).
    /// </remarks>
    public sealed class ProxyRequestCache
    {
        /// <summary>
        /// If true (default), the client's Cookie header is forwarded to the upstream server (including the server's session and device id cookies).
        /// </summary>
        public bool ForwardCookies { get; init; } = true;

        /// <summary>
        /// If true (default), the client's Authorization header is forwarded to the upstream server.
        /// </summary>
        public bool ForwardAuthorization { get; init; } = true;


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
        /// Handle a request by proxying it (or serving it from the cache) and writing the response to the request.
        /// Only GET and HEAD requests are cached as of now (POST caching would require a hash computation of the post data, even if the request isn't cached).
        /// </summary>
        /// <param name="context">The request, the response is written to it</param>
        /// <param name="req">The upstream url (part of the cache key)</param>
        /// <param name="doRequest">Function that performs a fresh request (as in not being cached)</param>
        /// <returns><see cref="HttpServerTools.AlreadyHandled"/></returns>
        /// <exception cref="HttpResponseException">Thrown (404) if the method isn't GET, HEAD or POST</exception>
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
            ProxyData reqInput = await ProxyTools.GetFromRequest(context).ConfigureAwait(false);
            if ((!ForwardCookies) || (!ForwardAuthorization))
                reqInput.Headers = RemoveHeaders(reqInput.Headers, !ForwardCookies, !ForwardAuthorization);
            ProxyData proxyRet = null;
            if (cache != null)
            {
                //  Responses may depend on the forwarded credentials, so they are part of the key
                String cookie = null;
                String auth = null;
                foreach (var h in reqInput.Headers.Nullable())
                {
                    if (h.StartsWith("Cookie:", StringComparison.OrdinalIgnoreCase))
                        cookie = cookie == null ? h : String.Concat(cookie, "\r", h);
                    else if (h.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase))
                        auth = auth == null ? h : String.Concat(auth, "\r", h);
                }
                var cacheKey = String.Join('\n', req, context.AcceptEncoding, cookie, auth);
                ProxyData fresh = null;
                var ce = await cache.GetOrUpdateWithExistingAsync(cacheKey, async (_, current) =>
                {
                    var r = await doRequest(req, reqInput).ConfigureAwait(false);
                    fresh = r;
                    proxyRet = r;
                    if (!GetCacheInfo(r, out var maxAge, out var etag, out var haveSetCookie))
                        return null;
                    var et = DateTime.UtcNow.AddSeconds(maxAge).Ticks;
                    if (r.StatusCode == 304)
                    {
                        if (current != null)
                        {
                            Interlocked.Exchange(ref current.Expires, et);
                            return current;
                        }
                        return null;
                    }
                    //  Never share cookies between clients
                    return new CacheEntry(et, etag, haveSetCookie ? new ProxyData(r.Method, RemoveSetCookie(r.Headers), r.Data, r.StatusCode) : r);
                }).ConfigureAwait(false);
                if (ce != null)
                {
                    var etag = ce.ETag;
                    if ((etag != null) && etag.FastEquals(context.IfNoneMatch))
                    {
                        context.SetResStatusCode(304);
                        return HttpServerTools.AlreadyHandled;
                    }
                    //  The request that made the upstream request gets the complete response (including any Set-Cookie headers)
                    proxyRet = ((fresh != null) && (fresh.StatusCode != 304)) ? fresh : ce.Data;
                }
            }
            if (proxyRet == null)
                proxyRet = await doRequest(req, reqInput).ConfigureAwait(false);
            await ProxyTools.SetToRequest(context, proxyRet).ConfigureAwait(false);
            return HttpServerTools.AlreadyHandled;
        }

        /// <summary>
        /// Get the caching information of an upstream response.
        /// </summary>
        /// <param name="r">The upstream response</param>
        /// <param name="maxAge">The max-age (in seconds) of the Cache-Control header(s)</param>
        /// <param name="etag">The ETag header value, null if none</param>
        /// <param name="haveSetCookie">True if the response contains Set-Cookie headers</param>
        /// <returns>True if the response may be stored in the (shared) cache, i.e it has a positive max-age and isn't "private" or "no-store"</returns>
        static bool GetCacheInfo(ProxyData r, out int maxAge, out String etag, out bool haveSetCookie)
        {
            etag = null;
            maxAge = 0;
            haveSetCookie = false;
            bool noStore = false;
            foreach (var header in r.Headers.Nullable())
            {
                if (header.StartsWith("Cache-Control:", StringComparison.OrdinalIgnoreCase))
                {
                    var values = header.Substring(14).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    foreach (var y in values)
                    {
                        if (y.StartsWith("max-age=", StringComparison.OrdinalIgnoreCase))
                        {
                            if (int.TryParse(y.AsSpan(8).Trim('"'), out var x))
                                if (x > maxAge)
                                    maxAge = x;
                            continue;
                        }
                        //  "private" may have field names (private="Set-Cookie"), treat as private
                        if (y.StartsWith("private", StringComparison.OrdinalIgnoreCase) || y.Equals("no-store", StringComparison.OrdinalIgnoreCase))
                            noStore = true;
                    }
                    continue;
                }
                if (header.StartsWith("ETag:", StringComparison.OrdinalIgnoreCase))
                {
                    etag = etag ?? header.Substring(5).Trim();
                    continue;
                }
                if (header.StartsWith("Set-Cookie:", StringComparison.OrdinalIgnoreCase))
                    haveSetCookie = true;
            }
            return (maxAge > 0) && (!noStore);
        }

        /// <summary>
        /// Get a copy of the headers without any Set-Cookie headers.
        /// </summary>
        static String[] RemoveSetCookie(String[] headers)
        {
            var l = new List<String>(headers.Length);
            foreach (var h in headers)
                if (!h.StartsWith("Set-Cookie:", StringComparison.OrdinalIgnoreCase))
                    l.Add(h);
            return l.ToArray();
        }

        /// <summary>
        /// Get a copy of the headers without the Cookie and / or Authorization headers.
        /// </summary>
        static String[] RemoveHeaders(String[] headers, bool removeCookies, bool removeAuthorization)
        {
            if (headers == null)
                return null;
            var l = new List<String>(headers.Length);
            foreach (var h in headers)
            {
                if (removeCookies && h.StartsWith("Cookie:", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (removeAuthorization && h.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase))
                    continue;
                l.Add(h);
            }
            return l.ToArray();
        }



        /// <summary>
        /// A cached upstream response.
        /// </summary>
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
