using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

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
                var c = WebTools.CreateHttpClient(p.UseTor, p.IgnoreCertErrors);
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
            }else
            {
                Client = (p.UseTor || p.IgnoreCertErrors) ? WebTools.CreateHttpClient(p.UseTor, p.IgnoreCertErrors) : WebTools.HttpClient;
            }
        }

        public void Dispose()
        {
            if (Client != WebTools.HttpClient)
                Client.Dispose();
        }

        readonly HttpClient Client;

        public override string ToString() => Name;

        readonly HttpCompressionPriority Comp;
        readonly int WebRootLen;
        readonly String SourceRoot;
        readonly int ClientCache;
        readonly int ServerCache;

        public IHttpRequestHandler Handler(HttpServerRequest context)
        {
            var u = context.LocalUrl;
            var mime = MimeTypeMap.GetMimeType(u.Substring(u.LastIndexOf('.') + 1));
            var req = SourceRoot + u.Substring(WebRootLen);
            var p = context.QueryParameters;
            if (p != null)
                req = String.Concat(req, '?', p);
            return new StaticStreamHttpRequestHandler(u,
                "Proxied",
                null,
                () => Client.GetStreamAsync(req),
                mime.Item1,
                mime.Item2 ? Comp : null, ClientCache, ServerCache);
        }


    }





}
