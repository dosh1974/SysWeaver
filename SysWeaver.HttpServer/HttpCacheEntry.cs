using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SysWeaver.Net
{
    sealed class HttpCacheEntry
    {
        public long LastUsed;
        public readonly int Status;
        public readonly String LocalUrl;
        public readonly long Expires;
        //public readonly HttpServerRequest Res;
        public readonly ReadOnlyMemory<Byte> Data;
        public readonly IReadOnlyList<KeyValuePair<String, IReadOnlyList<String>>> Headers;
        public readonly String ETag;

        public HttpCacheEntry(long lastUsed, long etag, HttpServerRequest res, ReadOnlyMemory<byte> data, String localUrl)
        {
            LastUsed = lastUsed;
            Expires = etag;
            Headers = res.AllResHeaders.ToList();
            Data = data;
            LocalUrl = localUrl;
            Status = res.GetResStatusCode();
        }

        /// <summary>
        /// Send the cached data
        /// </summary>
        /// <param name="data">The request to send it to</param>
        /// <param name="isHead">True if this is a HEAD request</param>
        /// <returns></returns>
        public ValueTask SendCached(HttpServerRequest data, bool isHead)
        {
            //  TODO: Handle range?
            data.SetResHeaders(Status, Headers);
            var b = Data;
            return (isHead || b.IsEmpty) ? ValueTask.CompletedTask : data.SetResBodyAsync(b);
        }

    }


}
