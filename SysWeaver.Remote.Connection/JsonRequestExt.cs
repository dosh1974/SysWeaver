using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using SysWeaver.Serialization;

namespace SysWeaver
{
    /// <summary>
    /// <see cref="HttpClient"/> extensions for POSTing json or raw binary payloads to an url (typically SysWeaver API end points).
    /// </summary>
    /// <remarks>
    /// All methods throw an <see cref="Exception"/> if the response status code isn't 200 (OK), and use the json serializer registered in <see cref="SerManager"/>.
    /// Raw payloads are sent with the content type <see cref="MimeTypeMap.Data"/>.
    /// </remarks>
    public static class JsonRequestExt
    {

        static readonly ISerializerType Ser = SerManager.Get("json");

        /// <summary>
        /// POST <paramref name="data"/> serialized as json, and deserialize the json response.
        /// </summary>
        /// <typeparam name="T">The request type.</typeparam>
        /// <typeparam name="R">The response type.</typeparam>
        /// <param name="client">The client to use.</param>
        /// <param name="url">The absolute url (or relative to the client base address).</param>
        /// <param name="data">The request object.</param>
        /// <returns>The deserialized response.</returns>
        /// <exception cref="Exception">The response status code isn't 200 (OK).</exception>
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


        /// <summary>
        /// POST <paramref name="data"/> serialized as json, and return the raw response body.
        /// </summary>
        /// <typeparam name="T">The request type.</typeparam>
        /// <param name="client">The client to use.</param>
        /// <param name="url">The absolute url (or relative to the client base address).</param>
        /// <param name="data">The request object.</param>
        /// <returns>The response body.</returns>
        /// <exception cref="Exception">The response status code isn't 200 (OK).</exception>
        public static async Task<ReadOnlyMemory<Byte>> PostJsonRequestRaw<T>(this HttpClient client, String url, T data)
        {
            using var c = new ReadOnlyMemoryContent(Ser.Serialize(data));
            c.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json", "utf-8");
            using var res = await client.PostAsync(url, c).ConfigureAwait(false);
            if (res.StatusCode != System.Net.HttpStatusCode.OK)
                throw new Exception("Request failed with: " + res.StatusCode + " [" + (int)res.StatusCode + "]");
            return await res.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// POST <paramref name="data"/> serialized as json, call <paramref name="onRaw"/> with the raw response, and deserialize the json response.
        /// </summary>
        /// <typeparam name="T">The request type.</typeparam>
        /// <typeparam name="R">The response type.</typeparam>
        /// <param name="client">The client to use.</param>
        /// <param name="url">The absolute url (or relative to the client base address).</param>
        /// <param name="data">The request object.</param>
        /// <param name="onRaw">Called with the response message (disposed after the call returns) and the raw body, before deserialization (ex: to read headers).</param>
        /// <returns>The deserialized response.</returns>
        /// <exception cref="Exception">The response status code isn't 200 (OK).</exception>
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


        /// <summary>
        /// POST <paramref name="data"/> serialized as json, call <paramref name="onRaw"/> with the raw response and return the raw response body.
        /// </summary>
        /// <typeparam name="T">The request type.</typeparam>
        /// <param name="client">The client to use.</param>
        /// <param name="url">The absolute url (or relative to the client base address).</param>
        /// <param name="data">The request object.</param>
        /// <param name="onRaw">Called with the response message (disposed after the call returns) and the raw body.</param>
        /// <returns>The response body.</returns>
        /// <exception cref="Exception">The response status code isn't 200 (OK).</exception>
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



        /// <summary>
        /// POST a raw binary payload and return the raw response body.
        /// </summary>
        /// <param name="client">The client to use.</param>
        /// <param name="url">The absolute url (or relative to the client base address).</param>
        /// <param name="data">The payload, sent as <see cref="MimeTypeMap.Data"/>.</param>
        /// <returns>The response body.</returns>
        /// <exception cref="Exception">The response status code isn't 200 (OK).</exception>
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

        /// <summary>
        /// POST a raw binary payload, call <paramref name="onRaw"/> with the raw response and return the raw response body.
        /// </summary>
        /// <param name="client">The client to use.</param>
        /// <param name="url">The absolute url (or relative to the client base address).</param>
        /// <param name="data">The payload, sent as <see cref="MimeTypeMap.Data"/>.</param>
        /// <param name="onRaw">Called with the response message (disposed after the call returns) and the raw body.</param>
        /// <returns>The response body.</returns>
        /// <exception cref="Exception">The response status code isn't 200 (OK).</exception>
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


        /// <summary>
        /// POST a raw binary payload and stream the response body (the response is not buffered, completes after headers are read).
        /// </summary>
        /// <param name="client">The client to use.</param>
        /// <param name="url">The absolute url (or relative to the client base address).</param>
        /// <param name="data">The payload, sent as <see cref="MimeTypeMap.Data"/>.</param>
        /// <param name="onResponse">Called with the response stream and the content length (if known), the stream is disposed when the returned task completes.</param>
        /// <returns>A task that completes when <paramref name="onResponse"/> has completed.</returns>
        /// <exception cref="Exception">The response status code isn't 200 (OK).</exception>
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

        /// <summary>
        /// POST a raw binary payload and let <paramref name="onResponse"/> process the (buffered) response.
        /// </summary>
        /// <typeparam name="T">The result type.</typeparam>
        /// <param name="client">The client to use.</param>
        /// <param name="url">The absolute url (or relative to the client base address).</param>
        /// <param name="data">The payload, sent as <see cref="MimeTypeMap.Data"/>.</param>
        /// <param name="onResponse">Called with the response message (disposed after the returned task completes) to produce the result.</param>
        /// <returns>The result of <paramref name="onResponse"/>.</returns>
        /// <exception cref="Exception">The response status code isn't 200 (OK).</exception>
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


