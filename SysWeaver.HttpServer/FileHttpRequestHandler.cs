using SysWeaver.Compression;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SysWeaver.Net
{
    /// <summary>
    /// A request handler that serves a single file from disc (optionally a pre-compressed copy of it).
    /// Created by <see cref="FileHttpServerModule"/> and by transformers that serve cached files.
    /// </summary>
    /// <remarks>
    /// The etag is computed from the last write time and size of the file, using the (possibly cached) state of the supplied <see cref="FileInfo"/>.
    /// Files up to 1 MiB are memory mapped, larger files are streamed.
    /// Instances don't hold any open file handles and can be shared between concurrent requests.
    /// </remarks>
    public sealed class FileHttpRequestHandler : IHttpRequestHandler
    {
        /// <summary>
        /// Returns the full path of the file.
        /// </summary>
        /// <returns>The full path of the file</returns>
        public override string ToString() => Fi.FullName;

        /// <summary>
        /// True if text template variable substitution may be applied, set when the mime type ends with "UTF-8" (text files).
        /// </summary>
        public bool AllowTemplates { get; init;  }


        /// <summary>
        /// Ignore, used internally
        /// </summary>
        public HttpServerRequest Redirected { get; set; }

        /// <summary>
        /// If true, the file's access time is updated whenever the file is read
        /// </summary>
        public readonly bool UpdateAccessTime;

        /// <summary>
        /// Create a handler that serves a file.
        /// </summary>
        /// <param name="mime">Mime type of the file and a flag that is true if the type is compressible (null to use the file extension)</param>
        /// <param name="fi">File information, the file should exist</param>
        /// <param name="options">Options for caching, compression and auth</param>
        /// <param name="isAccepted">True if the pre-compressed format (if any) is accepted by the client. Server side caching is only enabled if this is true or the file isn't pre-compressed</param>
        /// <param name="decoder">If the file is precompressed, this should be the decoder to use</param>
        /// <param name="updateAccessTime">If true, the file's access time is updated whenever the file is read</param>
        /// <param name="isDynamic">If true, no transformers will be applied</param>
        /// <remarks>
        /// Runtime compression and server side caching are only enabled for compressible mime types (or if <see cref="RequestOptions.ForceCache"/> is set),
        /// and server side caching only for files smaller than <see cref="RequestOptions.MaxCacheSize"/>.
        /// </remarks>
        public FileHttpRequestHandler(Tuple<String, bool> mime, FileInfo fi, RequestOptions options, bool isAccepted, ICompDecoder decoder = null, bool updateAccessTime = false, bool isDynamic = false)
        {
            if (mime == null)
                mime = MimeTypeMap.GetMimeType(fi.Extension);
            IsDynamic = isDynamic;
            UpdateAccessTime = updateAccessTime;
            Fi = fi;
            Decoder = decoder;
            if (mime.Item2 || options.ForceCache)
            {
                E = Encoding.UTF8;
                Compression = options.Compression;
                bool cache = (fi.Length < options.MaxCacheSize) && (isAccepted || (decoder == null));
                if (cache)
                    RequestCacheDuration = options.RequestCacheDuration;
            }
            AllowTemplates = mime.Item1.FastEndsWith("UTF-8");
            ClientCacheDuration = options.ClientCacheDuration;
            Mime = mime.Item1;
            Auth = options.Auth;
            IsLocalized = options.IsLocalized;
        }

        /// <summary>
        /// The text encoding (UTF-8) for compressible (text) files, else null.
        /// </summary>
        public readonly Encoding E;
        /// <summary>
        /// The mime type of the response.
        /// </summary>
        public readonly String Mime;
        /// <summary>
        /// The file being served (may be a pre-compressed variant of the requested file).
        /// </summary>
        public readonly FileInfo Fi;

        /// <inheritdoc/>
        public int ClientCacheDuration { get; init; }
        /// <inheritdoc/>
        public int RequestCacheDuration { get; init; }

        /// <inheritdoc/>
        public bool IsLocalized { get; init; }
        /// <inheritdoc/>
        public bool IsDynamic { get; init; }

        /// <inheritdoc/>
        public HttpCompressionPriority Compression { get; init; }

        /// <inheritdoc/>
        public ICompDecoder Decoder { get; init; }

        /// <inheritdoc/>
        public IReadOnlyList<String> Auth { get; init; }

        /// <summary>
        /// Returns null, so the request url is used as the server cache key.
        /// </summary>
        /// <param name="request">The request (not used)</param>
        /// <returns>A completed task with a null result</returns>
        public ValueTask<String> GetCacheKey(HttpServerRequest request) => TaskExt.NullStringValueTask;

        /// <summary>
        /// Sets the response mime type and returns an etag based on the last write time and size of the file.
        /// </summary>
        /// <param name="useAsync">Always false (<see cref="Get(HttpServerRequest)"/> is used)</param>
        /// <param name="request">The request</param>
        /// <returns>The etag</returns>
        public string GetEtag(out bool useAsync, HttpServerRequest request)
        {
            useAsync = false;
            request.SetResMime(Mime);
            var fi = Fi;
            return String.Concat(HttpServerTools.ToEtag(fi.LastWriteTimeUtc), ' ', HttpServerTools.ToEtag(fi.Length));
        }



        /// <summary>
        /// Open the file (updating the access time if <see cref="UpdateAccessTime"/> is true).
        /// </summary>
        /// <param name="request">The request (not used)</param>
        /// <returns>The file data, memory mapped if 1 MiB or smaller, else as a stream</returns>
        /// <exception cref="IOException">The file could not be opened (deleted, locked etc)</exception>
        public HttpRequestData Get(HttpServerRequest request)
        {
            var fi = Fi;
            if (UpdateAccessTime)
            {
                try
                {
                    fi.LastAccessTimeUtc = DateTime.UtcNow;
                }
                catch
                {
                }
            }
            var fs = fi.OpenRead();
            if (FileReadOnlyMemory.TryMap(out var mem, fs, true, 1 << 20))
                return new HttpRequestData(mem);
            return new HttpRequestData(fs);
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



    /// <summary>
    /// A request handler that produces the response body asynchronously using a delegate (ex: generated images).
    /// </summary>
    /// <remarks>
    /// No etag is returned, so 304 responses, templates and transformers are never used.
    /// Server side caching (keyed on the request url) is used if <see cref="RequestOptions.RequestCacheDuration"/> is non-zero.
    /// </remarks>
    public sealed class DynamicDataHttpRequestHandler : IHttpRequestHandler
    {

        /// <summary>
        /// Ignore, used internally
        /// </summary>
        public HttpServerRequest Redirected { get; set; }


        /// <summary>
        /// Create a handler that gets the response body from a delegate.
        /// </summary>
        /// <param name="mime">The mime type of the response (only Item1 is used)</param>
        /// <param name="getBody">Called (possibly concurrently) to get the response body for a request</param>
        /// <param name="options">Options for caching, compression, auth and localization</param>
        public DynamicDataHttpRequestHandler(Tuple<String, bool> mime, Func<HttpServerRequest, Task<ReadOnlyMemory<Byte>>> getBody, RequestOptions options)
        {
            RequestCacheDuration = options.RequestCacheDuration;
            ClientCacheDuration = options.ClientCacheDuration;
            Compression = options.Compression;
            Mime = mime.Item1;
            Auth = options.Auth;
            GetBody = getBody;
            IsLocalized = options.IsLocalized;
        }

        readonly Func<HttpServerRequest, Task<ReadOnlyMemory<Byte>>> GetBody;


        readonly String Mime;

        /// <inheritdoc/>
        public int ClientCacheDuration { get; init; }
        /// <inheritdoc/>
        public int RequestCacheDuration { get; init; }

        /// <inheritdoc/>
        public bool IsLocalized { get; init; }

        /// <inheritdoc/>
        public HttpCompressionPriority Compression { get; init; }

        /// <summary>
        /// The decoder of the data returned by the delegate if it's pre-compressed, null by default.
        /// </summary>
        public ICompDecoder Decoder { get; init; }

        /// <inheritdoc/>
        public IReadOnlyList<String> Auth { get; init; }

        /// <summary>
        /// Returns null, so the request url is used as the server cache key.
        /// </summary>
        /// <param name="request">The request (not used)</param>
        /// <returns>A completed task with a null result</returns>
        public ValueTask<String> GetCacheKey(HttpServerRequest request) => TaskExt.NullStringValueTask;

        /// <summary>
        /// Sets the response mime type, returns no etag and selects <see cref="GetAsync(HttpServerRequest)"/>.
        /// </summary>
        /// <param name="useAsync">Always true</param>
        /// <param name="request">The request</param>
        /// <returns>Always null</returns>
        public string GetEtag(out bool useAsync, HttpServerRequest request)
        {
            request.SetResMime(Mime);
            useAsync = true;
            return null;
        }

        /// <summary>
        /// Not supported, <see cref="GetAsync(HttpServerRequest)"/> is always used.
        /// </summary>
        /// <param name="request">The request</param>
        /// <returns>Never returns</returns>
        /// <exception cref="NotImplementedException">Always thrown</exception>
        public HttpRequestData Get(HttpServerRequest request)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Invokes the body delegate.
        /// </summary>
        /// <param name="request">The request</param>
        /// <returns>The response body</returns>
        public async Task<HttpRequestData> GetAsync(HttpServerRequest request)
        {
            return new HttpRequestData(await GetBody(request).ConfigureAwait(false));

        }

    }

}
