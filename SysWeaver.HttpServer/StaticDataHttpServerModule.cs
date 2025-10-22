using SysWeaver.Auth;
using SysWeaver.Compression;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace SysWeaver.Net
{







    /// <summary>
    /// A http module that can be used to serevr static web content
    /// </summary>
    public sealed class StaticDataHttpServerModule : IHttpServerModule
    {
        public StaticDataHttpServerModule(StaticDataHttpServerModuleParams mp = null, IMessageHost messageHandler = null)
        {
            var p = mp ?? new StaticDataHttpServerModuleParams();
            RootUri = p.UrlRoot?.Trim('/');
            ClientCacheDuration = Math.Max(0, p.ClientCacheDuration);
            Compression = HttpCompressionPriority.GetSupportedEncoders(p.Compression);
        }

        public override string ToString() => String.Concat(
            nameof(Handlers), ": ", Handlers.Count);

        readonly String RootUri;
        public readonly int ClientCacheDuration;
        public readonly HttpCompressionPriority Compression;

        const String LocationPrefix = "[Static] ";


        public delegate bool AddCondition(ref String name);


        static String PathFix(String x)
        {
            var i = x.LastIndexOf("/min.js");
            if (i < 0)
                return x;
            return String.Concat(x.Substring(0, i), ".min.js", x.Substring(i + 7));
        }

        /// <summary>
        /// Serve all matching embedded resources from an assembly
        /// </summary>
        /// <param name="asm">The assembly that contains the embedded resources</param>
        /// <param name="rootNamespace">Only resources starting with this string is served (and this is also removed from the path)</param>
        /// <param name="urlRoot">An optional path prefix to the resource name</param>
        /// <param name="clientCacheDuration">The client side cache duration (null to use the modules default)</param>
        /// <param name="compression">The runtime compression to apply (null to use the module default)</param>
        /// <param name="disableCompession">True to disable any compression</param>
        /// <param name="lastModified">The last modified string (null to use the default = application start)</param>
        /// <param name="auth">Required authorization tokens, null = no auth required, "" = auth required but no specific tokens, or comma separated list of required security tokens</param>
        /// <param name="doAdd">Optional function to determine if a resource should be included or not</param>
        /// <exception cref="NullReferenceException"></exception>
        public void AddEmbeddedResources(Assembly asm, String rootNamespace = null, String urlRoot = null, int? clientCacheDuration = null, HttpCompressionPriority compression = null, bool disableCompession = false, String lastModified = null, String auth = null, AddCondition doAdd = null)
        {
            var order = asm.GetCustomAttribute<ResourceOrderAttribute>()?.Order ?? 0;
            var names = asm.GetManifestResourceNames();
            var replace = asm.GetCustomAttribute<ReplaceEmbeddedFilesAttribute>()?.Replace ?? false;
            var pn = "Embedded resource \"";
            var sn = "\" from assembly \"" + asm.FullName?.Replace(", PublicKeyToken=null", "")?.Replace(", Culture=neutral", "") + "\"";
            foreach (var x in names)
            {
                var name = x;
                if (doAdd != null)
                {
                    if (!doAdd(ref name))
                        continue;
                }
                if (rootNamespace != null)
                {
                    if (!x.StartsWith(rootNamespace, StringComparison.Ordinal))
                        continue;
                    name = x.Substring(rootNamespace.Length + 1);
                }
                var s = name.Split('.');
                var sl = s.Length;
                --sl;
                var ext = s[sl];
                var extl = ext.FastToLower();
                ICompDecoder comp = CompManager.GetFromExt(extl);
                var location = String.Join(x, pn, sn);
                var mime = MimeTypeMap.GetMimeType(extl);
                if (comp != null)
                {
                    --sl;
                    AddStream(
                        HttpServerTools.CombinePaths(urlRoot, PathFix(String.Join('.', String.Join('/', s, 0, sl), s[sl], ext))),
                        location,
                        () => asm.GetManifestResourceStream(x) ?? throw new NullReferenceException(),
                        mime.Item1, 
                        clientCacheDuration,
                        0,
                        null,
                        true,
                        lastModified,
                        null,
                        auth,
                        replace,
                        order);
                    ext = s[sl];
                    extl = ext.FastToLower();
                    mime = MimeTypeMap.GetMimeType(extl);
                }
                AddStream(
                    HttpServerTools.CombinePaths(urlRoot, PathFix(String.Join('.', String.Join('/', s, 0, sl), ext))),
                    location,
                    () => asm.GetManifestResourceStream(x) ?? throw new NullReferenceException(),
                    mime.Item1,
                    clientCacheDuration,
                    HttpServerTools.MaxRequestCache,
                    compression,
                    disableCompession,
                    lastModified,
                    comp,
                    auth,
                    replace,
                    order);
            }
        }


        static String GetLocation(String def)
        {
            try
            {
                var f = new System.Diagnostics.StackTrace().GetFrame(2);
                if (f != null)
                {
                    var m = f.GetMethod();
                    if (m != null)
                        def += " registered by method \"" + m + "\"";
                    var fn = f.GetFileName();
                    if (fn != null)
                    {
                        def += " in \"" + fn + "\"";
                        var l = f.GetFileLineNumber();
                        if (l > 0)
                            def += " @ (" + l + ", " + f.GetFileColumnNumber() + ")";
                    }
                }
            }
            catch
            {
            }
            return def;
        }

        bool AddHandler(String url, IStaticHttpRequestHandler d, bool replace)
        {
            var h = Handlers;
            while (!h.TryAdd(url, d))
            {
                if (h.TryGetValue(url, out var e))
                {
                    var no = d.Order;
                    var eo = e.Order;
                    if (eo > no)
                        return false;
                    if ((eo == no) && (!replace))
                        return false;
                    h[url] = d;
                    break;
                }
            }
            return true;
        }

        /// <summary>
        /// Serve some stream (typically from a resouce)
        /// </summary>
        /// <param name="url">The url to serve it from</param>
        /// <param name="location">A string that describes the location of this asset, filename on disc, embedded resource name etc</param>
        /// <param name="openStream">The function to use for opening a stream</param>
        /// <param name="mime">The mime to use</param>
        /// <param name="clientCacheDuration">The client side cache duration (null to use the modules default)</param>
        /// <param name="requestCacheDuration">The server side cache duration</param>
        /// <param name="compression">The runtime compression to apply (null to use the module default)</param>
        /// <param name="disableCompession">True to disable any compression</param>
        /// <param name="lastModified">The last modified string (null to use the default = application start)</param>
        /// <param name="preCompressedFormat">The last modified string (null to use the default = application start)</param>
        /// <param name="auth">Required authorization tokens, null = no auth required, "" = auth required but no specific tokens, or comma separated list of required security tokens</param>
        /// <param name="replace">If true, any existing resource will be replaced if it's of the same order</param>
        /// <param name="order">An optional order, if the same resource is added more than once, the one with the highest order (or if equal the last replaced) is used</param>
        /// <returns>True if successful</returns>
        public bool AddStream(String url, String location, Func<Stream> openStream, String mime, int? clientCacheDuration = null, int requestCacheDuration = HttpServerTools.MaxRequestCache, HttpCompressionPriority compression = null, bool disableCompession = false, String lastModified = null, ICompDecoder preCompressedFormat = null, String auth = null, bool replace = false, double order = 0)
        {
            compression = disableCompession ? null : (compression ?? Compression);
            var dur = clientCacheDuration ?? ClientCacheDuration;
            var authTokens = Authorization.GetRequiredTokens(auth);
            long? len = null;
            using (var s = openStream())
            {
                try
                {
                    len = s.Length - s.Position;
                }
                catch
                {
                }
            }
            if (location == null)
                location = GetLocation("Stream");
            var d = new StaticStreamHttpRequestHandler(url, LocationPrefix + location, len, openStream, mime, compression, dur, requestCacheDuration, lastModified, preCompressedFormat, authTokens, order);
            url = HttpServerTools.CombinePaths(RootUri, url.Trim('/'));
            return AddHandler(url, d, replace);
        }

        /// <summary>
        /// Serve some memory
        /// </summary>
        /// <param name="url">The url to serve it from</param>
        /// <param name="location">A string that describes the location of this asset, filename on disc, embedded resource name etc</param>
        /// <param name="data">The data to serve</param>
        /// <param name="mime">The mime to use</param>
        /// <param name="clientCacheDuration">The client side cache duration (null to use the modules default)</param>
        /// <param name="compression">The runtime compression to apply (null to use the module default)</param>
        /// <param name="disableCompession">True to disable any compression</param>
        /// <param name="lastModified">The last modified string (null to use the default = application start)</param>
        /// <param name="preCompressedFormat">The last modified string (null to use the default = application start)</param>
        /// <param name="auth">Required authorization tokens, null = no auth required, "" = auth required but no specific tokens, or comma separated list of required security tokens</param>
        /// <param name="replace">If true, any existing resource will be replaced</param>
        /// <param name="order">An optional order, if the same resource is added more than once, the one with the highest order (or if equal the last replaced) is used</param>
        /// <returns>True if successful</returns>
        public bool AddMemory(String url, String location, ReadOnlyMemory<Byte> data, String mime, int? clientCacheDuration = null, HttpCompressionPriority compression = null, bool disableCompession = false, String lastModified = null, ICompDecoder preCompressedFormat = null, String auth = null, bool replace = false, double order = 0)
        {
            compression = disableCompession ? null : (compression ?? Compression);
            var dur = clientCacheDuration ?? ClientCacheDuration;
            var authTokens = auth == null ? null : Authorization.GetRequiredTokens(auth);
            if (location == null)
                location = GetLocation("Memory");
            var d = new StaticMemoryHttpRequestHandler(url, LocationPrefix + location, data, mime, compression, dur, HttpServerTools.MaxRequestCache, lastModified, preCompressedFormat, authTokens, order);
            url = HttpServerTools.CombinePaths(RootUri, url.Trim('/'));
            return AddHandler(url, d, replace);
        }

        /// <summary>
        /// Serve some text
        /// </summary>
        /// <param name="url">The url to serve it from</param>
        /// <param name="location">A string that describes the location of this asset, filename on disc, embedded resource name etc</param>
        /// <param name="text">The text to serve</param>
        /// <param name="mime">The mime to use</param>
        /// <param name="encoding">The text encoding to use (null to use default UTF8)</param>
        /// <param name="clientCacheDuration">The client side cache duration (null to use the modules default)</param>
        /// <param name="compression">The runtime compression to apply (null to use the module default)</param>
        /// <param name="disableCompession">True to disable any compression</param>
        /// <param name="lastModified">The last modified string (null to use the default = application start)</param>
        /// <param name="auth">Required authorization tokens, null = no auth required, "" = auth required but no specific tokens, or comma separated list of required security tokens</param>
        /// <param name="replace">If true, any existing resource will be replaced</param>
        /// <param name="order">An optional order, if the same resource is added more than once, the one with the highest order (or if equal the last replaced) is used</param>
        /// <param name="storeCompressed">If true, the data is stored compressed</param>
        /// <returns>True if successful</returns>
        public bool AddText(String url, String location, String text, String mime = MimeTypeMap.PlainText, Encoding encoding = null, int ? clientCacheDuration = null, HttpCompressionPriority compression = null, bool disableCompession = false, String lastModified = null, String auth = null, bool replace = false, double order = 0, bool storeCompressed = true)
        {
            compression = disableCompession ? null : (compression ?? Compression);
            var dur = clientCacheDuration ?? ClientCacheDuration;
            var e = encoding ?? Encoding.UTF8;
            ReadOnlyMemory<Byte> data = e.GetBytes(text);
            var authTokens = auth == null ? null : Authorization.GetRequiredTokens(auth);
            if (location == null)
                location = GetLocation("String");
            ICompType comp = null;
            if (storeCompressed)
            {
                comp = Comp;
                data = comp.GetCompressed(data.Span, CompEncoderLevels.Best);
            }
            var d = new StaticMemoryHttpRequestHandler(url, LocationPrefix + location, data, mime, compression, dur, HttpServerTools.MaxRequestCache, lastModified, comp, authTokens, order);
            url = HttpServerTools.CombinePaths(RootUri, url.Trim('/'));
            return AddHandler(url, d, replace);
        }

        static readonly ICompType Comp = CompManager.GetFromHttp("br");

        readonly SemiFrozenDictionary<String, IStaticHttpRequestHandler> Handlers = new SemiFrozenDictionary<string, IStaticHttpRequestHandler>(StringComparer.Ordinal);

        public bool Remove(String url) => Handlers.TryRemove(url, out var d);

        public bool Contains(String url) => Handlers.ContainsKey(url);

        /// <summary>
        /// Try to get a handler to a registered static resource
        /// </summary>
        /// <param name="localUrl">The local path to the resource</param>
        /// <returns>A handler for the resource or null if it doesn't exist</returns>
        public IStaticHttpRequestHandler TryGetHandler(String localUrl) => Handlers.TryGetValue(localUrl, out var d) ? d : null;

        public IHttpRequestHandler Handler(HttpServerRequest context)
        {
            Handlers.TryGetValue(context.LocalUrl, out var handler);
            return context.HttpMethod == HttpServerMethods.GET ? handler : null;
        }

        public IEnumerable<IHttpServerEndPoint> EnumEndPoints(String root = null)
        {
            if (root == null)
            {
                foreach (var x in Handlers)
                {
                    yield return (x.Value as IHttpServerEndPoint) ?? throw new NullReferenceException();
                }
            }else
            {
                root = HttpServerTools.FixEnumRoot(root);
                HashSet<String> folders = new HashSet<string>();
                var ul = root.Length;
                foreach (var x in Handlers)
                {
                    var url = x.Key;
                    if (!url.StartsWith(root, StringComparison.Ordinal))
                        continue;
                    var f = url.IndexOf('/', ul);
                    if (f < 0)
                        yield return (x.Value as IHttpServerEndPoint) ?? throw new NullReferenceException();
                    else
                    {
                        var folderName = url.Substring(ul, f - ul);
                        if (!folders.Add(folderName))
                            continue;
                        yield return new HttpServerEndPoint(root + folderName, "[Implicit Folder] from " + LocationPrefix, HttpServerTools.StartedTime);
                    }
                }
            }
        }


    }
}
