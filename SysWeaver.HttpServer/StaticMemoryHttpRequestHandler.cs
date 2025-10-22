using SysWeaver.Compression;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SysWeaver.Net
{
    /// <summary>
    /// A request handler static data (coming from a stream such as an embedded resource).
    /// </summary>
    public sealed class StaticMemoryHttpRequestHandler : IStaticHttpRequestHandler
    {
        /// <summary>
        /// Ignore, used internally
        /// </summary>
        public HttpServerRequest Redirected { get; set; }

        public bool AllowTemplates { get; init; }


        public StaticMemoryHttpRequestHandler(String uri, String location, ReadOnlyMemory<Byte> data, String mime, HttpCompressionPriority compression, int clientCacheDuration = 5, int requestCacheDuration = 0, String lastModified = null, ICompDecoder preCompressedFormat = null, IReadOnlyList<String> auth = null, double order = 0)
        {
            Order = order;
            Uri = uri;
            Location = location;
            Mime = mime;
            AllowTemplates = mime.FastEndsWith("UTF-8");
            ClientCacheDuration = clientCacheDuration;
            RequestCacheDuration = requestCacheDuration;
            Compression = compression;
            LastModified = lastModified ?? HttpServerTools.StartedText;
            Decoder = preCompressedFormat;
            CackeKey = HttpServerTools.GetStaticCacheUrl();
            Auth = auth;
            Data = data;
        }
        
        public readonly double Order;
        double IStaticHttpRequestHandler.Order => Order;

        public readonly String Mime;
        public readonly String LastModified;
        public readonly ReadOnlyMemory<Byte> Data;

        readonly ValueTask<String> CackeKey;

        public int ClientCacheDuration { get; private set; }
        public int RequestCacheDuration { get; private set; }
        public HttpCompressionPriority Compression { get; private set; }
        public ICompDecoder Decoder { get; private set; }
        public IReadOnlyList<String> Auth { get; private set; }
        public ValueTask<String> GetCacheKey(HttpServerRequest request) => CackeKey;
        public HttpServerEndpointTypes Type => HttpServerEndpointTypes.File;
        public bool UseStream => false;

        public ReadOnlyMemory<byte> GetData(HttpServerRequest request)
        {
            request.SetResMime(Mime);
            return Data;
        }

        public string GetEtag(out bool useAsync, HttpServerRequest request)
        {
            useAsync = false;
            return LastModified;
        }

        public Stream GetStream(HttpServerRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<Stream> GetStreamAsync(HttpServerRequest request)
        {
            throw new NotImplementedException();
        }
        public Task<ReadOnlyMemory<byte>> GetDataAsync(HttpServerRequest request)
        {
            throw new NotImplementedException();
        }

        public String Uri { get; private set; }
        public String Location { get; private set; }
        public long? Size => Data.Length;

        public String Method => "GET";

        public String CompPreference => Compression?.ToString();

        public String PreCompressed => Decoder?.HttpCode;

        DateTime IHttpServerEndPoint.LastModified => HttpServerTools.FromTimeStampString(LastModified);

        String IHttpServerEndPoint.Mime => Mime;
    }
}
