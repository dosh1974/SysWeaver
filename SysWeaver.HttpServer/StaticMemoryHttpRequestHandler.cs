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
            LastModifiedDate = HttpServerTools.FromTimeStampString(LastModified);
            Decoder = preCompressedFormat;
            CackeKey = HttpServerTools.GetStaticCacheUrl();
            CompPreference = compression?.ToString();
            PreCompressed = preCompressedFormat?.HttpCode;
            Size = data.Length;
            Auth = auth;
            Data = data;
        }

        public readonly double Order;
        double IStaticHttpRequestHandler.Order => Order;

        public readonly String Mime;
        public readonly String LastModified;
        public readonly DateTime LastModifiedDate;
        public readonly ReadOnlyMemory<Byte> Data;


        readonly ValueTask<String> CackeKey;

        public int ClientCacheDuration { get; init; }
        public int RequestCacheDuration { get; init; }
        public HttpCompressionPriority Compression { get; init; }
        public ICompDecoder Decoder { get; init; }
        public IReadOnlyList<String> Auth { get; init; }
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

        public String Uri { get; init; }
        public String Location { get; init; }
        
        public long? Size { get; init; } 

        public String Method => "GET";

        public String CompPreference { get; init; } 

        public String PreCompressed { get; init; } 

        DateTime IHttpServerEndPoint.LastModified => LastModifiedDate;

        String IHttpServerEndPoint.Mime => Mime;
    }
}
