using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using SysWeaver.Compression;

namespace SysWeaver.Net
{

    /// <summary>
    /// A simple request handler that responds with a fixed status code, mime type and body.
    /// Typically created using helpers such as <see cref="HttpServerTools.GetPlainTextHandler(string, int, Encoding)"/>.
    /// </summary>
    /// <remarks>
    /// The response is public (no auth), uncompressed, never cached on the server and cached for <see cref="ClientCacheDuration"/> seconds (default 5) on the client.
    /// Instances are immutable (except for the cache durations) and can be shared between requests.
    /// </remarks>
    public class GenericHttpRequestHandler : IHttpRequestHandler
    {

        /// <summary>
        /// Ignore, used internally
        /// </summary>
        public HttpServerRequest Redirected { get; set; }

        /// <summary>
        /// Create a generic http request handler that responds with some data.
        /// </summary>
        /// <param name="statusCode">The status code to return</param>
        /// <param name="mime">The mime type to use</param>
        /// <param name="data">The data to respond with (not copied, must not be modified afterwards)</param>
        public GenericHttpRequestHandler(int statusCode, String mime, ReadOnlyMemory<Byte> data)
        {
            StatusCode = statusCode;
            Mime = mime;
            Data = data;
        }


        /// <summary>
        /// Create a generic http request handler that responds with a status code and an empty body (no mime type).
        /// </summary>
        /// <param name="statusCode">The status code to return</param>
        public GenericHttpRequestHandler(int statusCode)
        {
            StatusCode = statusCode;
        }

        readonly int StatusCode;
        readonly String Mime;
        readonly ReadOnlyMemory<Byte> Data;

        /// <summary>
        /// The number of seconds the client may cache the response, defaults to 5.
        /// </summary>
        public int ClientCacheDuration { get; set; } = 5;

        /// <summary>
        /// The number of seconds the response is cached on the server, defaults to 0 (no server caching).
        /// </summary>
        public int RequestCacheDuration { get; set; } = 0;

        /// <summary>
        /// Always null (no runtime compression).
        /// </summary>
        public HttpCompressionPriority Compression => null;

        /// <summary>
        /// Always null (the data is not pre-compressed).
        /// </summary>
        public ICompDecoder Decoder => null;

        /// <summary>
        /// Always null (publicly available).
        /// </summary>
        public IReadOnlyList<string> Auth => null;

        /// <inheritdoc/>
        public ValueTask<String> GetCacheKey(HttpServerRequest request) => TaskExt.NullStringValueTask;


        /// <summary>
        /// Sets the response mime type and status code, returns a null etag (no 304 handling) and selects the synchronous <see cref="Get(HttpServerRequest)"/>.
        /// </summary>
        /// <param name="useAsync">Always false</param>
        /// <param name="request">The request</param>
        /// <returns>Always null</returns>
        public string GetEtag(out bool useAsync, HttpServerRequest request)
        {
            request.SetResMime(Mime);
            request.SetResStatusCode(StatusCode);
            useAsync = false;
            return null;
        }

        /// <summary>
        /// Returns the data supplied in the constructor.
        /// </summary>
        /// <param name="request">The request (not used)</param>
        /// <returns>The data</returns>
        public HttpRequestData Get(HttpServerRequest request)
        {
            return new HttpRequestData(Data, true);
        }

        /// <summary>
        /// Not supported, <see cref="Get(HttpServerRequest)"/> is always used.
        /// </summary>
        /// <param name="request">The request</param>
        /// <returns>Never returns</returns>
        /// <exception cref="NotImplementedException">Always thrown</exception>
        public async Task<HttpRequestData> GetAsync(HttpServerRequest request)
        {
            throw new NotImplementedException();
        }

    }





}
