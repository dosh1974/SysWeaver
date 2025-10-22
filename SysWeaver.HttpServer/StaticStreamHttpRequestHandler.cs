using SysWeaver.Compression;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace SysWeaver.Net
{

    public interface IStaticHttpRequestHandler : IHttpRequestHandler, IHttpServerEndPoint
    {
        double Order { get; }
    }

    /// <summary>
    /// A request handler static data (coming from a stream such as an embedded resource).
    /// </summary>
    public sealed class StaticStreamHttpRequestHandler : IStaticHttpRequestHandler
    {
        /// <summary>
        /// Ignore, used internally
        /// </summary>
        public HttpServerRequest Redirected { get; set; }

        public bool AllowTemplates { get; init; }

        public StaticStreamHttpRequestHandler(String uri, String location, long? length, Func<Stream> openStream, String mime, HttpCompressionPriority compression, int clientCacheDuration = 5, int requestCacheDuration = 0, String lastModified = null, ICompDecoder preCompressedFormat = null, IReadOnlyList<String> auth = null, double order = 0)
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
            LastModified = lastModified ?? HttpServerTools.StartedText;
            Decoder = preCompressedFormat;
            CackeKey = HttpServerTools.GetStaticCacheUrl();
            Auth = auth;
            OpenStream = openStream;
        }

        public StaticStreamHttpRequestHandler(String uri, String location, long? length, Func<Task<Stream>> openStreamAsync, String mime, HttpCompressionPriority compression, int clientCacheDuration = 5, int requestCacheDuration = 0, String lastModified = null, ICompDecoder preCompressedFormat = null, IReadOnlyList<String> auth = null, double order = 0)
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
            LastModified = lastModified ?? HttpServerTools.StartedText;
            Decoder = preCompressedFormat;
            CackeKey = HttpServerTools.GetStaticCacheUrl();
            Auth = auth;
            OpenStreamAsync = openStreamAsync;
        }

        public readonly double Order;
        double IStaticHttpRequestHandler.Order => Order;

        readonly String Mime;
        readonly String LastModified;
        readonly ValueTask<String> CackeKey;
        readonly Func<Stream> OpenStream;
        readonly Func<Task<Stream>> OpenStreamAsync;

        public int ClientCacheDuration { get; private set; }
        public int RequestCacheDuration { get; private set; }
        public HttpCompressionPriority Compression { get; private set; }
        public ICompDecoder Decoder { get; private set; }
        public IReadOnlyList<String> Auth { get; private set; }
        public ValueTask<String> GetCacheKey(HttpServerRequest request) => CackeKey;
        public HttpServerEndpointTypes Type => HttpServerEndpointTypes.File;
        public bool UseStream => true;

        public ReadOnlyMemory<byte> GetData(HttpServerRequest request)
        {
            throw new NotImplementedException();
        }

        public string GetEtag(out bool useAsync, HttpServerRequest request)
        {
            useAsync = OpenStreamAsync != null;
            return LastModified;
        }

        public Stream GetStream(HttpServerRequest request)
        {
            request.SetResMime(Mime);
            return OpenStream();
        }

        public Task<Stream> GetStreamAsync(HttpServerRequest request)
        {
            request.SetResMime(Mime);
            return OpenStreamAsync();
        }

        public Task<ReadOnlyMemory<byte>> GetDataAsync(HttpServerRequest request)
        {
            throw new NotImplementedException();
        }

        public String Uri { get; private set; }
        public String Location { get; private set; }
        public long? Size { get; private set; }

        public String Method => "GET";

        public String CompPreference => Compression?.ToString();

        public String PreCompressed => Decoder?.HttpCode;

        DateTime IHttpServerEndPoint.LastModified => HttpServerTools.FromTimeStampString(LastModified);

        String IHttpServerEndPoint.Mime => Mime;

    }
}
