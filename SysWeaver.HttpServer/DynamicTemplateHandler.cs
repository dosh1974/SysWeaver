using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using SysWeaver.Compression;

namespace SysWeaver.Net
{

    /*
    public sealed class DynamicTemplateHandler : IHttpRequestHandler
    {

        /// <summary>
        /// Ignore, used internally
        /// </summary>
        public HttpServerRequest Redirected { get; set; }

        /// <summary>
        /// Create a dynamic template handler (with uncacheable data)
        /// </summary>
        /// <param name="mime">The mime and a boolean indicating if it's a compressiable type</param>
        /// <param name="auth">The auth</param>
        /// <param name="compression">Compression (only used in the mime boolean is true)</param>
        /// <param name="text">The text template</param>
        public DynamicTemplateHandler(Tuple<String, bool> mime, IReadOnlyList<String> auth, HttpCompressionPriority compression, TextTemplate text)
        {
            Text = text;
            if (mime.Item2)
            {
                Compression = compression;
                E = Encoding.UTF8;
            }
            Mime = mime.Item1;
            Auth = auth;
        }

        readonly Encoding E;
        readonly String Mime;
        readonly TextTemplate Text;


        public int ClientCacheDuration => 0;
        public int RequestCacheDuration => 0;

        public bool UseStream => true;

        public HttpCompressionPriority Compression { get; private set; }

        public ICompDecoder Decoder => null;

        public IReadOnlyList<String> Auth { get; private set; }

        public Task<String> GetCacheKey(HttpServerRequest request) => HttpServerTools.NullStringTask;

        public ReadOnlyMemory<byte> GetData(HttpServerRequest request)
        {
            throw new NotImplementedException();
        }

        public string GetLastModified(out bool useAsync, HttpServerRequest request)
        {
            useAsync = false;
            return null;
        }

        public Stream GetStream(HttpServerRequest request)
        {
            request.SetResMime(Mime);
            var s = request.Server;
            var vars = HttpServerBase.GetVars(true, request);
            return request.Server.ApplyTemplate(Text, vars, null);
        }

        public Task<Stream> GetStreamAsync(HttpServerRequest request)
        {
            throw new NotImplementedException();
        }
        public Task<ReadOnlyMemory<byte>> GetDataAsync(HttpServerRequest request)
        {
            throw new NotImplementedException();
        }
    }

    */

}
