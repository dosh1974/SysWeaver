using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using SysWeaver.Compression;

namespace SysWeaver.Net
{
    /*
    public sealed class StaticTemplateHandler : IStaticHttpRequestHandler
    {
        /// <summary>
        /// Ignore, used internally
        /// </summary>
        public HttpServerRequest Redirected { get; set; }

        /// <summary>
        /// Create a static template handler (no dynamic variables)
        /// </summary>
        /// <param name="location">The mime and a boolean indicating if it's a compressiable type</param>
        /// <param name="uri">The auth</param>
        /// <param name="mime">The mime and a boolean indicating if it's a compressiable type</param>
        /// <param name="auth">The auth</param>
        /// <param name="compression">Compression (only used in the mime boolean is true)</param>
        /// <param name="clientCacheDuration">Duration to prevent a client to resend the request</param>
        /// <param name="requestCacheDuration">Duration to store the cached value on the server</param>
        /// <param name="text">The text template</param>
        /// <param name="getDateTime">A method to get the last modified time</param>
        /// <param name="order">An optional order, if the same resource is added more than once, the one with the highest order (or if equal the last replaced) is used</param>
        public StaticTemplateHandler(String uri, String location, Tuple<String, bool> mime, IReadOnlyList<String> auth, HttpCompressionPriority compression, int clientCacheDuration, int requestCacheDuration, TextTemplate text, Func<String> getDateTime = null, double order = 0)
        {
            Location = location;
            Uri = Uri;
            Order = order;
            Text = text;
            Size = text.Template.Length;
            if (mime.Item2)
            {
                Compression = compression;
                CompPreference = compression?.ToString();
                RequestCacheDuration = requestCacheDuration;
                E = Encoding.UTF8;
            }
            ClientCacheDuration = clientCacheDuration;
            Mime = mime.Item1;
            Auth = auth;
            var s = EnvInfo.AppStart.ToString("r");
            GetDateTime = getDateTime ?? new Func<String>(() => s);
        }

        public readonly double Order;
        double IStaticHttpRequestHandler.Order => Order;


        readonly Encoding E;
        readonly String Mime;
        readonly TextTemplate Text;
        readonly Func<String> GetDateTime;


        public int ClientCacheDuration { get; private set; }
        public int RequestCacheDuration { get; private set; }

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
            return GetDateTime();
        }

        public Stream GetStream(HttpServerRequest request)
        {
            request.SetResMime(Mime);
            var s = request.Server;
            var vars = HttpServerBase.GetVars(false, request);
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


        #region IHttpServerEndPoint
        public string Method => "GET";


        public HttpServerEndpointTypes Type => HttpServerEndpointTypes.File;

        public string CompPreference { get; init; }

        public string PreCompressed => null;

        public string Location { get; init; }

        public long? Size { get; init; }

        public DateTime LastModified => HttpServerTools.StartedTime;

        string IHttpServerEndPoint.Mime => Mime;

        public string Uri { get; init; }

        #endregion//IHttpServerEndPoint

    }


    */
}
