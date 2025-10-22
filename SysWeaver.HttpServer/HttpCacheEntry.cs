using System;
using System.Threading.Tasks;

namespace SysWeaver.Net
{
    sealed class HttpCacheEntry
    {
        public long LastUsed;
        public readonly String LocalUrl;
        public readonly long Expires;
        public readonly HttpServerRequest Res;
        public readonly ReadOnlyMemory<Byte> Data;

        public HttpCacheEntry(long lastUsed, long expires, HttpServerRequest res, ReadOnlyMemory<byte> data, String localUrl)
        {
            LastUsed = lastUsed;
            Expires = expires;
            Res = res;
            Data = data;
            LocalUrl = localUrl;
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
            Res.CopyHeaders(data);
            if (isHead)
                return ValueTask.CompletedTask;
            var b = Data;
            return b.IsEmpty ? ValueTask.CompletedTask : data.SetResBodyAsync(b);
        }

    }


}
