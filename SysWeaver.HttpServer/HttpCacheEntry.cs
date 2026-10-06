using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SysWeaver.Net
{
    /// <summary>
    /// A cached response (status, headers and body) stored in the global or per session request cache of <see cref="HttpServerBase"/>.
    /// </summary>
    /// <remarks>
    /// The body is copied to a managed array on first use (so that the source memory can be released) and then shared by all readers.
    /// Range requests are not handled when a response is served from the cache.
    /// </remarks>
    sealed class HttpCacheEntry
    {
        /// <summary>
        /// UTC ticks of the last time this entry was served (updated with Interlocked).
        /// </summary>
        public long LastUsed;
        /// <summary>
        /// The http status code of the cached response.
        /// </summary>
        public readonly int Status;
        /// <summary>
        /// The local url of the request that created the entry (used for invalidation and debug tables).
        /// </summary>
        public readonly String LocalUrl;
        /// <summary>
        /// UTC ticks when this entry expires.
        /// </summary>
        public readonly long Expires;
        //public readonly HttpServerRequest Res;
        ReadOnlyMemory<Byte> Data;
        volatile Byte[] DataBuf;
        /// <summary>
        /// The size of the cached body in bytes (materializes the body buffer).
        /// </summary>
        public int Length => GetBuffer().Length;

        /// <summary>
        /// A snapshot of the response headers when the entry was created (Set-Cookie headers are ignored when the entry is sent, see <see cref="HttpServerRequest.SetResHeaders"/>).
        /// </summary>
        public readonly IReadOnlyList<KeyValuePair<String, IReadOnlyList<String>>> Headers;
        //public readonly String ETag;

        /// <summary>
        /// Create a cache entry from a response that is being produced.
        /// </summary>
        /// <param name="lastUsed">UTC ticks of the creation time</param>
        /// <param name="etag">UTC ticks when the entry expires (despite the name)</param>
        /// <param name="res">The request whose response headers and status code are captured</param>
        /// <param name="data">The response body, must not be modified or released after this call</param>
        /// <param name="localUrl">The local url of the request</param>
        public HttpCacheEntry(long lastUsed, long etag, HttpServerRequest res, ReadOnlyMemory<byte> data, String localUrl)
        {
            LastUsed = lastUsed;
            Expires = etag;
            Headers = res.AllResHeaders.ToList();
            Data = data;
            LocalUrl = localUrl;
            Status = res.GetResStatusCode();
        }


        Byte[] GetBuffer()
        {
            var b = DataBuf;
            if (b == null)
            {
                lock (this)
                {
                    b = DataBuf;
                    if (b == null)
                    {
                        b = Data.ToArray();
                        DataBuf = b;
                        Data = null;
                    }
                }
            }
            return b;
        }


        /// <summary>
        /// Send the cached response (status, headers and body) to a request.
        /// </summary>
        /// <param name="data">The request to send it to</param>
        /// <param name="isHead">True if this is a HEAD request (no body is written)</param>
        /// <returns>A task that completes when the body has been written</returns>
        public Task SendCached(HttpServerRequest data, bool isHead)
        {
            //  TODO: Handle range?
            data.SetResHeaders(Status, Headers);
            var b = GetBuffer();
            var bl = b.Length;
            return (isHead || bl <= 0) ? Task.CompletedTask : data.SetResBodyAsync(b, 0, bl); 
            //var b = Data;
            //return (isHead || b.IsEmpty) ? Task.CompletedTask : data.SetResBodyAsync(b);
        }

    }


}
