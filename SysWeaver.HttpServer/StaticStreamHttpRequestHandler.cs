using SysWeaver.Compression;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace SysWeaver.Net
{

    /// <summary>
    /// A request handler for static content that also describes itself as an end point, used by <see cref="StaticDataHttpServerModule"/>.
    /// </summary>
    public interface IStaticHttpRequestHandler : IHttpRequestHandler, IHttpServerEndPoint
    {
        /// <summary>
        /// The order used when the same url is registered more than once, the handler with the highest order is used.
        /// </summary>
        double Order { get; }
    }

    /// <summary>
    /// A request handler that serves static data from a stream that is opened for every (uncached) request, such as an embedded resource.
    /// Also describes itself as an end point.
    /// </summary>
    /// <remarks>
    /// The stream factory is called concurrently and must return a new stream every time (the stream is disposed by the server).
    /// <see cref="IHttpRequestHandler.IsDynamic"/> is not overridden (true), so no transformers are applied.
    /// </remarks>
    public sealed class StaticStreamHttpRequestHandler : IStaticHttpRequestHandler
    {
        /// <summary>
        /// Ignore, used internally
        /// </summary>
        public HttpServerRequest Redirected { get; set; }

        /// <summary>
        /// True if text template variable substitution may be applied, set when the mime type ends with "UTF-8" (text).
        /// </summary>
        public bool AllowTemplates { get; init; }

        /// <summary>
        /// Create a handler that serves data from a synchronously opened stream.
        /// </summary>
        /// <param name="uri">The url used when describing the end point</param>
        /// <param name="location">A human readable description of where the data comes from</param>
        /// <param name="length">The length of the stream data (if known), only used for end point information</param>
        /// <param name="openStream">Opens a new stream with the data, called for every uncached request</param>
        /// <param name="mime">The mime type of the data (must not be null)</param>
        /// <param name="compression">Runtime compression to apply (null for none)</param>
        /// <param name="clientCacheDuration">Number of seconds the client may cache the response</param>
        /// <param name="requestCacheDuration">Number of seconds the response is cached on the server (0 = no caching)</param>
        /// <param name="lastModified">The last modified time (null to use the server start time)</param>
        /// <param name="etag">The etag to use (null to derive it from the last modified time)</param>
        /// <param name="preCompressedFormat">If the stream data is pre-compressed, the decoder of the compression format, else null</param>
        /// <param name="auth">The required security tokens (null = public, empty = any authenticated user)</param>
        /// <param name="order">The order used by <see cref="StaticDataHttpServerModule"/> when the same url is registered more than once</param>
        public StaticStreamHttpRequestHandler(String uri, String location, long? length, Func<Stream> openStream, String mime, HttpCompressionPriority compression, int clientCacheDuration = 5, int requestCacheDuration = 0, DateTime? lastModified = null, String etag = null, ICompDecoder preCompressedFormat = null, IReadOnlyList<String> auth = null, double order = 0)
        {
            Order = order;
            Uri = uri;
            Location = location;
            Size = length;
            Mime = mime;
            AllowTemplates = mime.FastEndsWith("UTF-8");
            ClientCacheDuration = clientCacheDuration;
            RequestCacheDuration = requestCacheDuration;
            Compression = compression;
            var lm = lastModified ?? HttpServerTools.StartedTime;
            LastModified = lm;
            ETag = etag ?? HttpServerTools.ToEtag(lm);
            Decoder = preCompressedFormat;
            CackeKey = HttpServerTools.GetStaticCacheUrl();
            Auth = auth;
            OpenStream = openStream;
        }

        /// <summary>
        /// Create a handler that serves data from an asynchronously opened stream.
        /// </summary>
        /// <param name="uri">The url used when describing the end point</param>
        /// <param name="location">A human readable description of where the data comes from</param>
        /// <param name="length">The length of the stream data (if known), only used for end point information</param>
        /// <param name="openStreamAsync">Opens a new stream with the data, called for every uncached request</param>
        /// <param name="mime">The mime type of the data (must not be null)</param>
        /// <param name="compression">Runtime compression to apply (null for none)</param>
        /// <param name="clientCacheDuration">Number of seconds the client may cache the response</param>
        /// <param name="requestCacheDuration">Number of seconds the response is cached on the server (0 = no caching)</param>
        /// <param name="lastModified">The last modified time (null to use the server start time)</param>
        /// <param name="etag">The etag to use (null to derive it from the last modified time)</param>
        /// <param name="preCompressedFormat">If the stream data is pre-compressed, the decoder of the compression format, else null</param>
        /// <param name="auth">The required security tokens (null = public, empty = any authenticated user)</param>
        /// <param name="order">The order used by <see cref="StaticDataHttpServerModule"/> when the same url is registered more than once</param>
        public StaticStreamHttpRequestHandler(String uri, String location, long? length, Func<Task<Stream>> openStreamAsync, String mime, HttpCompressionPriority compression, int clientCacheDuration = 5, int requestCacheDuration = 0, DateTime? lastModified = null, String etag = null, ICompDecoder preCompressedFormat = null, IReadOnlyList<String> auth = null, double order = 0)
        {
            Order = order;
            Uri = uri;
            Location = location;
            Size = length;
            Mime = mime;
            AllowTemplates = mime.FastEndsWith("UTF-8");
            ClientCacheDuration = clientCacheDuration;
            RequestCacheDuration = requestCacheDuration;
            Compression = compression;
            var lm = lastModified ?? HttpServerTools.StartedTime;
            LastModified = lm;
            ETag = etag ?? HttpServerTools.ToEtag(lm);
            Decoder = preCompressedFormat;
            CackeKey = HttpServerTools.GetStaticCacheUrl();
            Auth = auth;
            OpenStreamAsync = openStreamAsync;
        }

        /// <summary>
        /// The order used when the same url is registered more than once (highest wins).
        /// </summary>
        public readonly double Order;
        double IStaticHttpRequestHandler.Order => Order;

        readonly String Mime;
        readonly String ETag;
        readonly ValueTask<String> CackeKey;
        readonly Func<Stream> OpenStream;
        readonly Func<Task<Stream>> OpenStreamAsync;

        /// <inheritdoc/>
        public int ClientCacheDuration { get; private set; }
        /// <inheritdoc/>
        public int RequestCacheDuration { get; private set; }
        /// <inheritdoc/>
        public HttpCompressionPriority Compression { get; private set; }
        /// <inheritdoc/>
        public ICompDecoder Decoder { get; private set; }
        /// <inheritdoc/>
        public IReadOnlyList<String> Auth { get; private set; }
        /// <summary>
        /// Returns a server cache key that is unique to this handler instance.
        /// </summary>
        /// <param name="request">The request (not used)</param>
        /// <returns>The cache key</returns>
        public ValueTask<String> GetCacheKey(HttpServerRequest request) => CackeKey;
        /// <summary>
        /// Always <see cref="HttpServerEndpointTypes.File"/>.
        /// </summary>
        public HttpServerEndpointTypes Type => HttpServerEndpointTypes.File;
        /// <summary>
        /// Sets the response mime type and returns the etag, selects <see cref="GetAsync(HttpServerRequest)"/> if the handler was created with an async stream factory.
        /// </summary>
        /// <param name="useAsync">True if the async stream factory is used</param>
        /// <param name="request">The request</param>
        /// <returns>The etag</returns>
        public string GetEtag(out bool useAsync, HttpServerRequest request)
        {
            request.SetResMime(Mime);
            useAsync = OpenStreamAsync != null;
            return ETag;
        }

        /// <summary>
        /// Opens a new stream using the synchronous stream factory.
        /// </summary>
        /// <param name="request">The request (not used)</param>
        /// <returns>The stream data (owned by the caller)</returns>
        public HttpRequestData Get(HttpServerRequest request)
        {
            return new HttpRequestData(OpenStream());
        }

        /// <summary>
        /// Opens a new stream using the asynchronous stream factory.
        /// </summary>
        /// <param name="request">The request (not used)</param>
        /// <returns>The stream data (owned by the caller)</returns>
        public async Task<HttpRequestData> GetAsync(HttpServerRequest request)
        {
            return new HttpRequestData(await OpenStreamAsync().ConfigureAwait(false));
        }

        /// <inheritdoc/>
        public String Uri { get; private set; }
        /// <inheritdoc/>
        public String Location { get; private set; }
        /// <inheritdoc/>
        public long? Size { get; private set; }

        /// <summary>
        /// Always "GET".
        /// </summary>
        public String Method => "GET";

        /// <inheritdoc/>
        public String CompPreference => Compression?.ToString();

        /// <inheritdoc/>
        public String PreCompressed => Decoder?.HttpCode;

        /// <inheritdoc/>
        public DateTime LastModified { get; init; }

        String IHttpServerEndPoint.Mime => Mime;

        String IHttpServerEndPoint.ETag => ETag;

    }
}
