using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using SysWeaver.Compression;

namespace SysWeaver.Net
{

    /// <summary>
    /// A simple generic http request handler, use the HttpServer.Get - helpers
    /// </summary>
    public class GenericHttpRequestHandler : IHttpRequestHandler
    {

        /// <summary>
        /// Ignore, used internally
        /// </summary>
        public HttpServerRequest Redirected { get; set; }

        /// <summary>
        /// Create a generic http request handler
        /// </summary>
        /// <param name="statusCode">The status code to return</param>
        /// <param name="mime">The mime type to use</param>
        /// <param name="data">The data to respond with</param>
        /// <param name="contentEncoding">The content encoding that was used for the data (if it's text)</param>
        public GenericHttpRequestHandler(int statusCode, String mime, ReadOnlyMemory<Byte> data, Encoding contentEncoding = null)
        {
            StatusCode = statusCode;
            Mime = mime;
            Data = data;
            ContentEncoding = contentEncoding;
        }

        readonly int StatusCode;
        readonly Encoding ContentEncoding;
        readonly String Mime;
        readonly ReadOnlyMemory<Byte> Data;

        public int ClientCacheDuration { get; set; } = 5;

        public int RequestCacheDuration { get; set; } = 0;

        public bool UseStream => false;

        public HttpCompressionPriority Compression => null;

        public ICompDecoder Decoder => null;

        public IReadOnlyList<string> Auth => null;

        public ValueTask<String> GetCacheKey(HttpServerRequest request) => HttpServerTools.NullStringValueTask;


        public string GetEtag(out bool useAsync, HttpServerRequest request)
        {
            useAsync = false;
            return null;
        }

        public virtual ReadOnlyMemory<byte> GetData(HttpServerRequest request)
        {
            request.SetResMime(Mime);
            request.SetResStatusCode(StatusCode);
            return Data;
        }

        public Task<ReadOnlyMemory<byte>> GetDataAsync(HttpServerRequest request)
        {
            throw new NotImplementedException();
        }

        public Stream GetStream(HttpServerRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<Stream> GetStreamAsync(HttpServerRequest request)
        {
            throw new NotImplementedException();
        }
    }





}
