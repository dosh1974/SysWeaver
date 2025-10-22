using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using SysWeaver.Compression;
using SysWeaver.Net;



namespace SysWeaver.MicroService
{
    sealed class QrResponseHandler : IHttpRequestHandler
    {
        /// <summary>
        /// Ignore, used internally
        /// </summary>
        public HttpServerRequest Redirected { get; set; }

        public QrResponseHandler(QrCodeService s, String svg)
        {
            Compression = s.ResponseComp;
            Auth = s.ResponseAuth;
            Data = Encoding.UTF8.GetBytes(svg);
        }

        readonly Byte[] Data;


        public int ClientCacheDuration => WebApiTools.CacheClientStatic;

        public int RequestCacheDuration => 30 * 60;

        public bool UseStream => false;


        public HttpCompressionPriority Compression { get; init; }

        public ICompDecoder Decoder => null;

        public IReadOnlyList<string> Auth { get; init; }

        public ValueTask<String> GetCacheKey(HttpServerRequest request) => HttpServerTools.NullStringValueTask;


        public string GetEtag(out bool useAsync, HttpServerRequest request)
        {
            useAsync = false;
            return HttpServerTools.StartedText;
        }

        public ReadOnlyMemory<byte> GetData(HttpServerRequest request)
        {
            request.SetResMime("image/svg+xml; charset=UTF-8");
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
