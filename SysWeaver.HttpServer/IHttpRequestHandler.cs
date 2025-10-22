using SysWeaver.Compression;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SysWeaver.Net
{

    public interface IHttpRequestHandler
    {

        /// <summary>
        /// The name of this request handler
        /// </summary>
        String Name { get => GetType().Name; }

        /// <summary>
        /// Implementations should never care or set this property
        /// </summary>
        HttpServerRequest Redirected { get; set; }

        /// <summary>
        /// The duration in seconds that the client should keep the response cached (basically setting up the Cache header in the response)
        /// </summary>
        int ClientCacheDuration { get; }
        
        /// <summary>
        /// The duration in seconds that the same request should be cached on the server, i.e the WriteStream / GetData for the same request from multiple clients within this period will only result in a single call to these methods (reduces server load).
        /// If negative, the per session cache is used (else a global cache is used).
        /// </summary>
        int RequestCacheDuration { get; }

        /// <summary>
        /// True if the input is from a stream
        /// </summary>
        bool UseStream { get; }

        /// <summary>
        /// If true, the response is localized (language dependent)
        /// </summary>
        bool IsLocalized => false;

        /// <summary>
        /// The compression method to use (in order of preferens) or null if no compression should be applied
        /// </summary>
        HttpCompressionPriority Compression { get; }

        /// <summary>
        /// If the data is compressed, set the decoder here
        /// </summary>
        ICompDecoder Decoder { get; }

        /// <summary>
        /// Auth token required to access this (null to make it publically available)
        /// </summary>
        IReadOnlyList<String> Auth { get; }

        /// <summary>
        /// Can optionally return a unique cache key 
        /// </summary>
        /// <param name="request">The request information</param>
        /// <returns>A unique cache key, must start with ':'</returns>
        ValueTask<String> GetCacheKey(HttpServerRequest request);

        /// <summary>
        /// Get the etag (typically last modified string, using the HttpServerTools.ToEtag method).
        /// </summary>
        /// <param name="useAsync">If true, the async version of GetStream or GetData is used</param>
        /// <param name="request">The request information</param>
        /// <returns>An etag, only use [A-Z], [a-z], [0-9], '_', '-'</returns>
        String GetEtag(out bool useAsync, HttpServerRequest request);
        
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

        /// <summary>
        /// True to allow template matching to be used
        /// </summary>
        bool AllowTemplates => false;

        /// <summary>
        /// An optional rate limiter for this handler for the whole service
        /// </summary>
        HttpRateLimiter ServiceRateLimiter => null;


        /// <summary>
        /// An optional rate limiter per session (handled and stored in session storage by the handler)
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        HttpRateLimiter SessionRateLimiter(HttpSession session) => null;


    }
}
