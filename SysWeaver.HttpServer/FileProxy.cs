using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using SysWeaver.Auth;
using SysWeaver.Compression;

namespace SysWeaver.Net
{
    public sealed class FileProxy : IHttpServerModule, IDisposable
    {

        public String Name { get; init; }

        public String[] OnlyForPrefixes { get; init; }

        public FileProxy(FileProxyParams p)
        {
            var root = p.WebRoot;
            var sourceRoot = p.SourceRoot;
            if (String.IsNullOrEmpty(root))
                throw new Exception("Web root may not be empty!");
            if (String.IsNullOrEmpty(sourceRoot))
                throw new Exception("Source root may not be empty!");
            Name = String.Concat("FileProxy ", root, " => ", sourceRoot);
            WebRootLen = root.Length;
            SourceRoot = sourceRoot;
            Comp = HttpCompressionPriority.GetSupportedEncoders(p.Compression);
            ClientCache = Math.Max(p.ClientCacheDuration, 0);
            ServerCache = Math.Max(p.ServerCacheDuration, 0);
            if (!root.FastEquals("/"))
                OnlyForPrefixes = [root];
            if (p.GetUserPassword(out var user, out var password, false))
            {
                OwnClient = true;
                var c = WebTools.CreateHttpClient(p.UseTor, p.IgnoreCertErrors, false);
                Client = c;
                if (user.FastToLower().FastEquals("bearer"))
                {
                    c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", password);
                }
                else
                {
                    var byteArray = Encoding.ASCII.GetBytes(String.Join(":", user, password));
                    c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

                }
            }
            else
            {
                Client = WebTools.GetSharedHttpClient(p.UseTor, p.IgnoreCertErrors, false);
            }
            AsyncHandler = HandleAsync;
            Auth = Authorization.GetRequiredTokens(p.Auth);
        }

        readonly IReadOnlyList<String> Auth;

        public void Dispose()
        {
            if (OwnClient)
                Client.Dispose();
        }

        readonly bool OwnClient;
        readonly HttpClient Client;

        public override string ToString() => Name;

        readonly HttpCompressionPriority Comp;
        readonly int WebRootLen;
        readonly String SourceRoot;
        readonly int ClientCache;
        readonly int ServerCache;


        static readonly String[] Headers = [
            "Accept-Language",
            "If-None-Match",
            "Accept-Encoding",
            "Content-Encoding",
            "Content-Type",
            "Accept",
            ];

        public Func<HttpServerRequest, ValueTask<IHttpRequestHandler>> AsyncHandler { get; init; }

        async ValueTask<IHttpRequestHandler> HandleAsync(HttpServerRequest context)
        {
            var u = context.LocalUrl;
            var req = SourceRoot + u.Substring(WebRootLen);
#if DEBUG
            String loc = String.Concat("Proxied from \"", req, '"');
#else//DEBUG
            const String loc = "Proxied";
#endif//DEBUG
            var p = context.QueryStringStart;
            if (p > 0)
                req = String.Concat(req, '?', context.Url.Substring(p));
            var m = new HttpRequestMessage
            {
                Content = new StreamContent(context.InputStream),
                Method = new HttpMethod(context.Method),
                RequestUri = new Uri(req),
            };
            var sh = m.Headers;
            foreach (var x in Headers)
            {
                var val = context.GetReqHeader(x);
                if (val != null)
                    sh.TryAddWithoutValidation(x, val);
            }
            var d = await Client.SendAsync(m).ConfigureAwait(false);
            var sc = d.StatusCode;
            var isc = (int)sc;
            if (!d.IsSuccessStatusCode)
                throw new HttpResponseException(isc, sc.ToString());
            if (isc == 304)
            {
                context.SetResStatusCode(304);
                return HttpServerTools.AlreadyHandled;
            }
            var rh = d.Content.Headers;
            var slength = rh.GetValues("Content-Length")?.FirstOrDefault();
            long? len = null;
            if (slength != null)
                if (long.TryParse(slength, out var xx))
                    len = xx;
            DateTime? lwt = null;
            String etag = null;
            if (rh.TryGetValues("ETag", out var xxx))
            {
                etag = xxx?.FirstOrDefault();
                lwt = HttpServerTools.TryGetDateTimeFromETag(etag);
            }

            var mime = rh?.ContentType?.MediaType;
            var doComp = MimeTypeMap.GetMimeType(mime ?? "").Item2;
            var compression = rh.ContentEncoding?.FirstOrDefault();
            ICompDecoder cmp = null;
            if (!String.IsNullOrEmpty(compression))
            {
                cmp = CompManager.GetFromHttp(compression);
                if (cmp == null)
                    throw new Exception("Unsupported compression method");
            }
            return new StaticStreamHttpRequestHandler(u,
                loc,
                len,
                () => d.Content.ReadAsStream(),
                mime,
                doComp ? Comp : null, 
                ClientCache, 
                ServerCache,
                lwt,
                etag,
                cmp, 
                Auth);
        }


    }






}
