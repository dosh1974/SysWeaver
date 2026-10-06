using SysWeaver.Compression;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SysWeaver.Net
{

    /// <summary>
    /// Produces the response for a request, returned by an <see cref="IHttpServerModule"/>.
    /// The properties describe how the server should treat the response (auth, client and server caching, compression, templates, transformers, rate limiting),
    /// the server performs those steps and calls <see cref="Get(HttpServerRequest)"/> or <see cref="GetAsync(HttpServerRequest)"/> to get the actual data.
    /// </summary>
    /// <remarks>
    /// Handlers are frequently cached and shared between concurrent requests, so implementations must be thread safe and should not store per request state
    /// (the exception being <see cref="Redirected"/>, which is only set on per request handler instances).
    /// Order of calls: <see cref="Auth"/> is checked, then <see cref="GetEtag(out bool, HttpServerRequest)"/> is called (304 handling), then <see cref="GetCacheKey(HttpServerRequest)"/> (if server caching is enabled) and finally Get or GetAsync on a cache miss.
    /// </remarks>
    public interface IHttpRequestHandler
    {

        /// <summary>
        /// The name of this request handler (defaults to the type name), used for performance tracking.
        /// </summary>
        String Name { get => GetType().Name; }

        /// <summary>
        /// If non-null, the server processes this request instead of the incoming one (and disposes it if it's <see cref="IDisposable"/>).
        /// Used internally when a request is redirected/rewritten, implementations should only store the value.
        /// </summary>
        HttpServerRequest Redirected { get; set; }

        /// <summary>
        /// The duration in seconds that the client should keep the response cached (basically setting up the Cache-Control header in the response).
        /// </summary>
        int ClientCacheDuration { get; }
        
        /// <summary>
        /// The duration in seconds that the same request should be cached on the server, i.e Get / GetAsync for the same request from multiple clients within this period will only result in a single call to these methods (reduces server load).
        /// Zero disables server side caching. If negative, the per session cache is used with the absolute value as duration (else a global cache is used).
        /// </summary>
        /// <remarks>
        /// The cache key is built from <see cref="GetCacheKey(HttpServerRequest)"/> (or the url), the request type, the Accept-Encoding header, the http method and (for localized content) the language.
        /// Responses that differ per user must use a negative duration or a user specific cache key.
        /// </remarks>
        int RequestCacheDuration { get; }

        /// <summary>
        /// If true, the response is localized (language dependent) and the session language is made part of the server cache key.
        /// </summary>
        bool IsLocalized => false;

        /// <summary>
        /// The compression methods to use (in order of preference) or null if no runtime compression should be applied.
        /// </summary>
        HttpCompressionPriority Compression { get; }

        /// <summary>
        /// If the data returned by Get / GetAsync is pre-compressed, the decoder of that compression format, else null.
        /// The server sends the data as is if the client accepts the encoding, else it decompresses (and possibly re-compresses) it.
        /// </summary>
        ICompDecoder Decoder { get; }

        /// <summary>
        /// The security tokens required to access this: null = publicly available, empty = any authenticated user, else the listed tokens are required
        /// (typically created using <see cref="SysWeaver.Auth.Authorization.GetRequiredTokens(string)"/>).
        /// </summary>
        IReadOnlyList<String> Auth { get; }

        /// <summary>
        /// Can optionally return a unique server cache key for the response (only called when <see cref="RequestCacheDuration"/> is non-zero).
        /// </summary>
        /// <param name="request">The request information</param>
        /// <returns>A unique cache key that should start with ':' (so that it can't collide with an url), null to use the request url as the key, or an empty string to disable server caching for this request</returns>
        ValueTask<String> GetCacheKey(HttpServerRequest request);

        /// <summary>
        /// Get the etag (typically based on the last modified time, using <see cref="HttpServerTools.ToEtag(DateTime)"/>).
        /// Called once per request (after the auth check), implementations typically also set the response mime type (and status code) here.
        /// </summary>
        /// <param name="useAsync">Set to true to make the server call <see cref="GetAsync(HttpServerRequest)"/>, false to call <see cref="Get(HttpServerRequest)"/></param>
        /// <param name="request">The request information</param>
        /// <returns>An etag (only use [A-Z], [a-z], [0-9], '_', '-' and ' '), or null if the content has no stable version (disables 304 responses, templates and transformers)</returns>
        String GetEtag(out bool useAsync, HttpServerRequest request);

        /// <summary>
        /// Set to true if the data is dynamic. If false, mime/extension based transformers (see <see cref="IHttpTransformerService"/>) may be applied to the data.
        /// Defaults to true.
        /// </summary>
        bool IsDynamic => true;

        /// <summary>
        /// Get the data for a request (called when <see cref="GetEtag(out bool, HttpServerRequest)"/> returned useAsync = false).
        /// </summary>
        /// <param name="request">The request information</param>
        /// <returns>The response data, ownership is transferred to the server (it's disposed after use)</returns>
        HttpRequestData Get(HttpServerRequest request);


        /// <summary>
        /// Get the data for a request (called when <see cref="GetEtag(out bool, HttpServerRequest)"/> returned useAsync = true).
        /// </summary>
        /// <param name="request">The request information</param>
        /// <returns>The response data, ownership is transferred to the server (it's disposed after use)</returns>
        Task<HttpRequestData> GetAsync(HttpServerRequest request);

        /*

        /// <summary>
        /// True if the input is from a stream
        /// </summary>
        bool UseStream { get; }

        /// <summary>
        /// Get the data stream, only call if UseStream is true
        /// </summary>
        /// <param name="request">The request information</param>
        /// <returns>A data stream</returns>
        Stream GetStream(HttpServerRequest request);

        /// <summary>
        /// Get the data stream, only call if UseStream is true
        /// </summary>
        /// <param name="request">The request information</param>
        /// <returns>A data stream</returns>
        Task<Stream> GetStreamAsync(HttpServerRequest request);

        /// <summary>
        /// Get the data memory, only call if UseStream is false
        /// </summary>
        /// <param name="request">The request information</param>
        /// <returns>A data memory</returns>
        ReadOnlyMemory<Byte> GetData(HttpServerRequest request);

        /// <summary>
        /// Get the data memory, only call if UseStream is false
        /// </summary>
        /// <param name="request">The request information</param>
        /// <returns>A data memory</returns>
        Task<ReadOnlyMemory<Byte>> GetDataAsync(HttpServerRequest request);

*/
        /// <summary>
        /// True to allow text template variable substitution ("${Variable}") to be applied to the response (only if an etag is returned).
        /// Defaults to false.
        /// </summary>
        bool AllowTemplates => false;

        /// <summary>
        /// An optional rate limiter for this handler, shared by all sessions (null for no limit).
        /// </summary>
        HttpRateLimiter ServiceRateLimiter => null;


        /// <summary>
        /// An optional rate limiter per session (handled and stored in session storage by the handler).
        /// </summary>
        /// <param name="session">The session of the current request</param>
        /// <returns>The rate limiter to use for this session, or null for no limit</returns>
        HttpRateLimiter SessionRateLimiter(HttpSession session) => null;


    }
}
