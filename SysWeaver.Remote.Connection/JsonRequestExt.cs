using System;
using System.Net.Http;
using System.Threading.Tasks;
using SysWeaver.Serialization;

namespace SysWeaver
{
    public static class JsonRequestExt
    {
        public static async Task<R> PostJsonRequest<T, R>(this HttpClient client, String url, T data)
        {
            var j = SerManager.Get("json");
            using var c = new ReadOnlyMemoryContent(j.Serialize(data));
            c.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json", "utf-8");
            using var res = await client.PostAsync(url, c).ConfigureAwait(false);
            if (res.StatusCode != System.Net.HttpStatusCode.OK)
                throw new Exception("Request failed with: " + res.StatusCode + " [" + (int)res.StatusCode + "]");
            var ret = await res.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            return j.Create<R>(ret.AsSpan());
        }
    }


}


