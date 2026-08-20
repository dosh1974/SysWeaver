using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using SysWeaver.Serialization;

namespace SysWeaver
{
    public static class JsonRequestExt
    {

        static readonly ISerializerType Ser = SerManager.Get("json");

        public static async Task<R> PostJsonRequest<T, R>(this HttpClient client, String url, T data)
        {
            var j = Ser;
            using var c = new ReadOnlyMemoryContent(j.Serialize(data));
            c.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json", "utf-8");
            using var res = await client.PostAsync(url, c).ConfigureAwait(false);
            if (res.StatusCode != System.Net.HttpStatusCode.OK)
                throw new Exception("Request failed with: " + res.StatusCode + " [" + (int)res.StatusCode + "]");
            var ret = await res.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            return j.Create<R>(ret.AsSpan());
        }


        public static async Task<ReadOnlyMemory<Byte>> PostJsonRequestRaw<T>(this HttpClient client, String url, T data)
        {
            using var c = new ReadOnlyMemoryContent(Ser.Serialize(data));
            c.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json", "utf-8");
            using var res = await client.PostAsync(url, c).ConfigureAwait(false);
            if (res.StatusCode != System.Net.HttpStatusCode.OK)
                throw new Exception("Request failed with: " + res.StatusCode + " [" + (int)res.StatusCode + "]");
            return await res.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        }

        public static async Task<R> PostJsonRequest<T, R>(this HttpClient client, String url, T data, Func<HttpResponseMessage, ReadOnlyMemory<Byte>, Task> onRaw)
        {
            var j = Ser;
            using var c = new ReadOnlyMemoryContent(j.Serialize(data));
            c.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json", "utf-8");
            using var res = await client.PostAsync(url, c).ConfigureAwait(false);
            if (res.StatusCode != System.Net.HttpStatusCode.OK)
                throw new Exception("Request failed with: " + res.StatusCode + " [" + (int)res.StatusCode + "]");
            var ret = await res.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            await onRaw(res, ret).ConfigureAwait(false);
            return j.Create<R>(ret.AsSpan());
        }


        public static async Task<ReadOnlyMemory<Byte>> PostJsonRequestRaw<T>(this HttpClient client, String url, T data, Func<HttpResponseMessage, ReadOnlyMemory<Byte>, Task> onRaw)
        {
            using var c = new ReadOnlyMemoryContent(Ser.Serialize(data));
            c.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json", "utf-8");
            using var res = await client.PostAsync(url, c).ConfigureAwait(false);
            if (res.StatusCode != System.Net.HttpStatusCode.OK)
                throw new Exception("Request failed with: " + res.StatusCode + " [" + (int)res.StatusCode + "]");
            var ret = await res.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            await onRaw(res, ret).ConfigureAwait(false);
            return ret;
        }



        public static async Task<ReadOnlyMemory<Byte>> PostRawRequestRaw(this HttpClient client, String url, ReadOnlyMemory<Byte> data)
        {
            using var c = new ReadOnlyMemoryContent(data);
            c.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(MimeTypeMap.Data);
            using var res = await client.PostAsync(url, c).ConfigureAwait(false);
            if (res.StatusCode != System.Net.HttpStatusCode.OK)
                throw new Exception("Request failed with: " + res.StatusCode + " [" + (int)res.StatusCode + "]");
            var ret = await res.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            return ret;
        }

        public static async Task<ReadOnlyMemory<Byte>> PostRawRequestRaw(this HttpClient client, String url, ReadOnlyMemory<Byte> data, Func<HttpResponseMessage, ReadOnlyMemory<Byte>, Task> onRaw)
        {
            using var c = new ReadOnlyMemoryContent(data);
            c.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(MimeTypeMap.Data);
            using var res = await client.PostAsync(url, c).ConfigureAwait(false);
            if (res.StatusCode != System.Net.HttpStatusCode.OK)
                throw new Exception("Request failed with: " + res.StatusCode + " [" + (int)res.StatusCode + "]");
            var ret = await res.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            await onRaw(res, ret).ConfigureAwait(false);
            return ret;
        }


        public static async Task PostRawRequestStream(this HttpClient client, String url, ReadOnlyMemory<Byte> data, Func<Stream, long?, Task> onResponse)
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, url);
            using var c = new ReadOnlyMemoryContent(data);
            c.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(MimeTypeMap.Data);
            message.Content = c;
            using var res = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            if (res.StatusCode != System.Net.HttpStatusCode.OK)
                throw new Exception("Request failed with: " + res.StatusCode + " [" + (int)res.StatusCode + "]");
            var cc = res.Content;
            using var stream = await cc.ReadAsStreamAsync().ConfigureAwait(false);
            await onResponse(stream, cc.Headers.ContentLength).ConfigureAwait(false);
        }

        public static async Task<T> PostRawRequest<T>(this HttpClient client, String url, ReadOnlyMemory<Byte> data, Func<HttpResponseMessage, Task<T>> onResponse)
        {
            using var c = new ReadOnlyMemoryContent(data);
            c.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(MimeTypeMap.Data);
            using var res = await client.PostAsync(url, c).ConfigureAwait(false);
            if (res.StatusCode != System.Net.HttpStatusCode.OK)
                throw new Exception("Request failed with: " + res.StatusCode + " [" + (int)res.StatusCode + "]");
            return await onResponse(res).ConfigureAwait(false);
        }

    }


}


