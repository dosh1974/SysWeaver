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
        ReadOnlyMemory<Byte> Data;
        volatile Byte[] DataBuf;
        public int Length => GetBuffer().Length;

        public readonly IReadOnlyList<KeyValuePair<String, IReadOnlyList<String>>> Headers;
        //public readonly String ETag;

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
        /// Send the cached data
        /// </summary>
        /// <param name="data">The request to send it to</param>
        /// <param name="isHead">True if this is a HEAD request</param>
        /// <returns></returns>
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
