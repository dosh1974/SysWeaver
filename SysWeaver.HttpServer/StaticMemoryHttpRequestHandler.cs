using SysWeaver.Compression;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SysWeaver.Net
{
    /// <summary>
    /// A request handler that serves static data from memory (ex: an embedded resource or generated content).
    /// Also describes itself as an end point.
    /// </summary>
    /// <remarks>
    /// Immutable, can be shared between concurrent requests.
    /// </remarks>
    public sealed class StaticMemoryHttpRequestHandler : IStaticHttpRequestHandler
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
        /// Create a handler that serves some memory.
        /// </summary>
        /// <param name="uri">The url used when describing the end point</param>
        /// <param name="location">A human readable description of where the data comes from</param>
        /// <param name="data">The data to serve (not copied, must not be modified afterwards)</param>
        /// <param name="mime">The mime type of the data</param>
        /// <param name="compression">Runtime compression to apply (null for none)</param>
        /// <param name="clientCacheDuration">Number of seconds the client may cache the response</param>
        /// <param name="requestCacheDuration">Number of seconds the response is cached on the server (0 = no caching)</param>
        /// <param name="lastModified">The last modified time (null to use the server start time)</param>
        /// <param name="etag">The etag to use (null to compute it from a hash of the data, combined with the last modified time if supplied)</param>
        /// <param name="preCompressedFormat">If the data is pre-compressed, the decoder of the compression format, else null</param>
        /// <param name="auth">The required security tokens (null = public, empty = any authenticated user)</param>
        /// <param name="order">The order used by <see cref="StaticDataHttpServerModule"/> when the same url is registered more than once</param>
        /// <param name="isDynamic">If true, no transformers are applied</param>
        public StaticMemoryHttpRequestHandler(String uri, String location, ReadOnlyMemory<Byte> data, String mime, HttpCompressionPriority compression, int clientCacheDuration = 5, int requestCacheDuration = 0, DateTime? lastModified = null, String etag = null, ICompDecoder preCompressedFormat = null, IReadOnlyList<String> auth = null, double order = 0, bool isDynamic = false)
        {
            Order = order;
            Uri = uri;
            Location = location;
            Mime = mime;
            AllowTemplates = mime?.FastEndsWith("UTF-8") ?? false;
            ClientCacheDuration = clientCacheDuration;
            RequestCacheDuration = requestCacheDuration;
            Compression = compression;
            var lm = lastModified ?? HttpServerTools.StartedTime;
            LastModified = lm;
            ETag = etag ?? (lastModified == null ? HttpServerTools.ToEtag(data.Span) : String.Concat(HttpServerTools.ToEtag(lm), ' ', HttpServerTools.ToEtag(data.Span)));
            Decoder = preCompressedFormat;
            CackeKey = HttpServerTools.GetStaticCacheUrl();
            CompPreference = compression?.ToString();
            PreCompressed = preCompressedFormat?.HttpCode;
            Size = data.Length;
            Auth = auth;
            Data = data;
            IsDynamic = isDynamic;
        }

        /// <summary>
        /// The order used when the same url is registered more than once (highest wins).
        /// </summary>
        public readonly double Order;
        double IStaticHttpRequestHandler.Order => Order;

        /// <summary>
        /// The mime type of the data.
        /// </summary>
        public readonly String Mime;
        /// <summary>
        /// The etag of the data.
        /// </summary>
        public readonly String ETag;
        /// <summary>
        /// The data that is served (possibly pre-compressed, see <see cref="Decoder"/>).
        /// </summary>
        public readonly ReadOnlyMemory<Byte> Data;



        /// <inheritdoc/>
        public bool IsDynamic { get; init; }


        readonly ValueTask<String> CackeKey;

        /// <inheritdoc/>
        public int ClientCacheDuration { get; init; }
        /// <inheritdoc/>
        public int RequestCacheDuration { get; init; }
        /// <inheritdoc/>
        public HttpCompressionPriority Compression { get; init; }
        /// <inheritdoc/>
        public ICompDecoder Decoder { get; init; }
        /// <inheritdoc/>
        public IReadOnlyList<String> Auth { get; init; }
        /// <summary>
        /// Returns a server cache key that is unique to this handler instance (so the same data is shared by all urls/requests that use it).
        /// </summary>
        /// <param name="request">The request (not used)</param>
        /// <returns>The cache key</returns>
        public ValueTask<String> GetCacheKey(HttpServerRequest request) => CackeKey;
        /// <summary>
        /// Always <see cref="HttpServerEndpointTypes.File"/>.
        /// </summary>
        public HttpServerEndpointTypes Type => HttpServerEndpointTypes.File;

        /// <summary>
        /// Returns the data.
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

        /// <summary>
        /// Sets the response mime type and returns the etag.
        /// </summary>
        /// <param name="useAsync">Always false</param>
        /// <param name="request">The request</param>
        /// <returns>The etag</returns>
        public string GetEtag(out bool useAsync, HttpServerRequest request)
        {
            request.SetResMime(Mime);
            useAsync = false;
            return ETag;
        }

        /// <inheritdoc/>
        public String Uri { get; init; }
        /// <inheritdoc/>
        public String Location { get; init; }
        
        /// <summary>
        /// The size of the data in bytes (as stored, i.e compressed size if pre-compressed).
        /// </summary>
        public long? Size { get; init; } 

        /// <summary>
        /// Always "GET".
        /// </summary>
        public String Method => "GET";

        /// <inheritdoc/>
        public String CompPreference { get; init; } 

        /// <inheritdoc/>
        public String PreCompressed { get; init; }

        /// <inheritdoc/>
        public DateTime LastModified { get; init; }

        String IHttpServerEndPoint.Mime => Mime;

        string IHttpServerEndPoint.ETag => ETag;
    }
}
