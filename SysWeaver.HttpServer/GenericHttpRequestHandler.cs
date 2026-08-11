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
        public GenericHttpRequestHandler(int statusCode, String mime, ReadOnlyMemory<Byte> data)
        {
            StatusCode = statusCode;
            Mime = mime;
            Data = data;
        }


        /// <summary>
        /// Create a generic http request handler
        /// </summary>
        /// <param name="statusCode">The status code to return</param>
        public GenericHttpRequestHandler(int statusCode)
        {
            StatusCode = statusCode;
        }

        readonly int StatusCode;
        readonly String Mime;
        readonly ReadOnlyMemory<Byte> Data;

        public int ClientCacheDuration { get; set; } = 5;

        public int RequestCacheDuration { get; set; } = 0;

        public HttpCompressionPriority Compression => null;

        public ICompDecoder Decoder => null;

        public IReadOnlyList<string> Auth => null;

        public ValueTask<String> GetCacheKey(HttpServerRequest request) => HttpServerTools.NullStringValueTask;


        public string GetEtag(out bool useAsync, HttpServerRequest request)
        {
            request.SetResMime(Mime);
            request.SetResStatusCode(StatusCode);
            useAsync = false;
            return null;
        }

        public HttpRequestData Get(HttpServerRequest request)
        {
            return new HttpRequestData(Data, true);
        }

        public async ValueTask<HttpRequestData> GetAsync(HttpServerRequest request)
        {
            throw new NotImplementedException();
        }

    }





}
