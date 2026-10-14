using SysWeaver.Auth;
using SysWeaver.Compression;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace SysWeaver.Net
{







    /// <summary>
    /// A http module that serves static content registered at runtime (embedded resources, memory, text or streams), keyed on the exact local url (GET requests only).
    /// </summary>
    /// <remarks>
    /// Typically used to serve the embedded web assets of all loaded assemblies.
    /// If the same url is registered more than once, the handler with the highest order wins (on equal order, the last one wins only if replace is requested).
    /// By default registered content is cached on the server "forever" (the request cache duration is <see cref="HttpServerTools.MaxRequestCache"/>), the cache key is unique per registration.
    /// Thread safe.
    /// </remarks>
    public sealed class StaticDataHttpServerModule : IHttpServerModule
    {
        /// <summary>
        /// Create a static data module.
        /// </summary>
        /// <param name="mp">Parameters (null to use defaults)</param>
        /// <param name="messageHandler">Not used</param>
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
        /// <summary>
        /// The default number of seconds a client may cache a resource (used when no duration is supplied when adding).
        /// </summary>
        public readonly int ClientCacheDuration;
        /// <summary>
        /// The default runtime compression (used when no compression is supplied when adding).
        /// </summary>
        public readonly HttpCompressionPriority Compression;

        const String LocationPrefix = "[Static] ";

        const String ImplicitFolderLocation = "[Implicit Folder] from " + LocationPrefix;


        /// <summary>
        /// A filter used by <see cref="AddEmbeddedResources"/>.
        /// </summary>
        /// <param name="name">The manifest resource name, may be modified to change the served name</param>
        /// <returns>True to include the resource, false to skip it</returns>
        public delegate bool AddCondition(ref String name);

        /// <summary>
        /// A filter used by <see cref="AddEmbeddedResources(Assembly, String, String, int?, HttpCompressionPriority, bool, DateTime?, String, String, AddStartCondition)"/>, the same as an <see cref="AddCondition"/> that renames a resource to the end of its name (no string per resource).
        /// </summary>
        /// <param name="name">The manifest resource name</param>
        /// <returns>The index of the first char of the served name in <paramref name="name"/> (0 to use the whole name), or a negative value to skip the resource</returns>
        public delegate int AddStartCondition(String name);


        static String PathFix(String x)
        {
            var i = x.FastLastIndexOf("/min.js");
            if (i < 0)
                return x;
            return String.Concat(x.Substring(0, i), ".min.js", x.Substring(i + 7));
        }

        /// <summary>
        /// Serve all matching embedded resources from an assembly.
        /// The resource name is turned into a url by replacing all '.' (except the one before the extension) with '/', ex: "web.scripts.app.js" becomes "web/scripts/app.js" (and ".../min.js" becomes "....min.js").
        /// </summary>
        /// <param name="asm">The assembly that contains the embedded resources</param>
        /// <param name="rootNamespace">Only resources starting with this string (followed by a '.') are served, and this prefix is removed from the path</param>
        /// <param name="urlRoot">An optional path prefix to the resource name</param>
        /// <param name="clientCacheDuration">The client side cache duration (null to use the modules default)</param>
        /// <param name="compression">The runtime compression to apply (null to use the module default)</param>
        /// <param name="disableCompession">True to disable any compression</param>
        /// <param name="lastModified">The last modified time (null to use the last write time of the assembly)</param>
        /// <param name="etag">The etag to use for all resources (null to derive it from the last modified time)</param>
        /// <param name="auth">Required authorization tokens, null = no auth required, "" = auth required but no specific tokens, or comma separated list of required security tokens</param>
        /// <param name="doAdd">Optional function to determine if a resource should be included or not (it may also rename the resource, the root namespace is stripped from the new name if it starts with it)</param>
        /// <remarks>
        /// Resources with a compression extension (ex: "app.js.br") are served both as is and as the uncompressed name ("app.js", with the data marked as pre-compressed).
        /// The <see cref="ResourceOrderAttribute"/> and <see cref="ReplaceEmbeddedFilesAttribute"/> of the assembly control which assembly wins if several assemblies supply the same url.
        /// The resource data is read into memory.
        /// </remarks>
        public void AddEmbeddedResources(Assembly asm, String rootNamespace = null, String urlRoot = null, int? clientCacheDuration = null, HttpCompressionPriority compression = null, bool disableCompession = false, DateTime? lastModified = null, String etag = null,String auth = null, AddCondition doAdd = null)
        {
            var lwt = lastModified ?? asm.GetLastWriteTimerUtc();
            etag = etag ?? HttpServerTools.ToEtag(lwt);
            var order = asm.GetCustomAttribute<ResourceOrderAttribute>()?.Order ?? 0;
            var names = asm.GetManifestResourceNames();
            var replace = asm.GetCustomAttribute<ReplaceEmbeddedFilesAttribute>()?.Replace ?? false;
            var sn = "\" from assembly \"" + asm.FullName?.Replace(", PublicKeyToken=null", "")?.Replace(", Culture=neutral", "") + "\"";
            //  The values that AddMemory computes from the arguments (the same for all resources)
            var runtimeComp = disableCompession ? null : (compression ?? Compression);
            var dur = clientCacheDuration ?? ClientCacheDuration;
            IReadOnlyList<String> authTokens = null;
            foreach (var x in names)
            {
                var name = x;
                if (doAdd != null)
                {
                    if (!doAdd(ref name))
                        continue;
                }
                //  The served name is name.Substring(start)
                int start = 0;
                if (rootNamespace != null)
                {
                    var nsl = rootNamespace.Length;
                    if ((x.Length <= nsl) || (x[nsl] != '.') || (!x.StartsWith(rootNamespace, StringComparison.Ordinal)))
                        continue;
                    //  Strip the namespace from the (possibly renamed) name
                    if ((name.Length > nsl) && (name[nsl] == '.') && name.StartsWith(rootNamespace, StringComparison.Ordinal))
                        start = nsl + 1;
                }
                if (!AddEmbeddedResource(asm, x, name, start, sn, urlRoot, dur, runtimeComp, disableCompession, lwt, etag, auth, ref authTokens, replace, order))
                    AddEmbeddedResourceSplit(asm, x, name.Substring(start), sn, urlRoot, clientCacheDuration, compression, disableCompession, lwt, etag, auth, replace, order);
            }
        }

        /// <summary>
        /// Serve all matching embedded resources from an assembly, the same as <see cref="AddEmbeddedResources(Assembly, String, String, int?, HttpCompressionPriority, bool, DateTime?, String, String, AddCondition)"/>
        /// with a filter that can only rename a resource to the end of its manifest name (no string is created for the served name).
        /// The resource name is turned into a url by replacing all '.' (except the one before the extension) with '/', ex: "web.scripts.app.js" becomes "web/scripts/app.js" (and ".../min.js" becomes "....min.js").
        /// </summary>
        /// <param name="asm">The assembly that contains the embedded resources</param>
        /// <param name="rootNamespace">Only resources starting with this string (followed by a '.') are served, and this prefix is removed from the path</param>
        /// <param name="urlRoot">An optional path prefix to the resource name</param>
        /// <param name="clientCacheDuration">The client side cache duration (null to use the modules default)</param>
        /// <param name="compression">The runtime compression to apply (null to use the module default)</param>
        /// <param name="disableCompession">True to disable any compression</param>
        /// <param name="lastModified">The last modified time (null to use the last write time of the assembly)</param>
        /// <param name="etag">The etag to use for all resources (null to derive it from the last modified time)</param>
        /// <param name="auth">Required authorization tokens, null = no auth required, "" = auth required but no specific tokens, or comma separated list of required security tokens</param>
        /// <param name="getStart">A function that returns the index in the manifest resource name where the served name starts (the root namespace is stripped from the served name if it starts with it), or a negative value to skip the resource, null to include all resources as is</param>
        /// <remarks>
        /// Resources with a compression extension (ex: "app.js.br") are served both as is and as the uncompressed name ("app.js", with the data marked as pre-compressed).
        /// The <see cref="ResourceOrderAttribute"/> and <see cref="ReplaceEmbeddedFilesAttribute"/> of the assembly control which assembly wins if several assemblies supply the same url.
        /// The resource data is read into memory.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="getStart"/> returns an index greater than the length of the name.</exception>
        public void AddEmbeddedResources(Assembly asm, String rootNamespace, String urlRoot, int? clientCacheDuration, HttpCompressionPriority compression, bool disableCompession, DateTime? lastModified, String etag, String auth, AddStartCondition getStart)
        {
            var lwt = lastModified ?? asm.GetLastWriteTimerUtc();
            etag = etag ?? HttpServerTools.ToEtag(lwt);
            var order = asm.GetCustomAttribute<ResourceOrderAttribute>()?.Order ?? 0;
            var names = asm.GetManifestResourceNames();
            var replace = asm.GetCustomAttribute<ReplaceEmbeddedFilesAttribute>()?.Replace ?? false;
            var sn = "\" from assembly \"" + asm.FullName?.Replace(", PublicKeyToken=null", "")?.Replace(", Culture=neutral", "") + "\"";
            //  The values that AddMemory computes from the arguments (the same for all resources)
            var runtimeComp = disableCompession ? null : (compression ?? Compression);
            var dur = clientCacheDuration ?? ClientCacheDuration;
            IReadOnlyList<String> authTokens = null;
            foreach (var x in names)
            {
                //  The served name is x.Substring(start)
                int start = 0;
                if (getStart != null)
                {
                    start = getStart(x);
                    if (start < 0)
                        continue;
                    if (start > x.Length)
                        throw new ArgumentOutOfRangeException(nameof(getStart));
                }
                if (rootNamespace != null)
                {
                    var nsl = rootNamespace.Length;
                    if ((x.Length <= nsl) || (x[nsl] != '.') || (!x.StartsWith(rootNamespace, StringComparison.Ordinal)))
                        continue;
                    //  Strip the namespace from the served name
                    var name = x.AsSpan(start);
                    if ((name.Length > nsl) && (name[nsl] == '.') && name.StartsWith(rootNamespace, StringComparison.Ordinal))
                        start += nsl + 1;
                }
                if (!AddEmbeddedResource(asm, x, x, start, sn, urlRoot, dur, runtimeComp, disableCompession, lwt, etag, auth, ref authTokens, replace, order))
                    AddEmbeddedResourceSplit(asm, x, x.Substring(start), sn, urlRoot, clientCacheDuration, compression, disableCompession, lwt, etag, auth, replace, order);
            }
        }

        /// <summary>
        /// "[Static] " (the prefix that AddMemory adds to the location) + "Embedded resource \""
        /// </summary>
        const String EmbeddedLocationPrefix = LocationPrefix + "Embedded resource \"";

        /// <summary>
        /// Keys up to this length are lower cased on the stack
        /// </summary>
        const int MaxStackExt = 256;

        /// <summary>
        /// Add an embedded resource (the same as <see cref="AddEmbeddedResourceSplit"/> with fewer allocations: no split parts and joins, one location string, the resource data is read once).
        /// </summary>
        /// <param name="name">A string that contains the served name</param>
        /// <param name="start">The index of the served name in <paramref name="name"/> (the served name is the rest of the string)</param>
        /// <returns>False if nothing was done because the name is unusual (no '.', a compressed name with a single '.' or a long extension), use <see cref="AddEmbeddedResourceSplit"/> for those</returns>
        [SkipLocalsInit]
        bool AddEmbeddedResource(Assembly asm, String x, String name, int start, String sn, String urlRoot, int dur, HttpCompressionPriority runtimeComp, bool disableCompession, DateTime lwt, String etag, String auth, ref IReadOnlyList<String> authTokens, bool replace, double order)
        {
            var p = name.AsSpan(start);
            var ld = p.LastIndexOf('.');
            if (ld < 0)
                return false;
            var ext = p.Slice(ld + 1);
            var el = ext.Length;
            if (el > MaxStackExt)
                return false;
            Span<Char> extl = stackalloc Char[el];
            ext.ToLowerInvariant(extl);
            ICompDecoder comp = CompManager.GetFromExt(extl);
            //  The index of the first '.' that is kept in the url (the dots before it become '/')
            var keep = ld;
            int ld2 = 0;
            if (comp != null)
            {
                ld2 = p.Slice(0, ld).LastIndexOf('.');
                if (ld2 < 0)
                    return false;
                keep = ld2;
            }
            var mime = MimeTypeMap.GetMimeType(extl);
            //  The location as AddMemory stores it ("[Static] " + location), shared by both urls of a compressed resource
            var location = String.Concat(EmbeddedLocationPrefix, x, sn);
            if (auth != null)
                authTokens ??= Authorization.GetRequiredTokens(auth);
            //  Both urls of a compressed resource serve the same (immutable) data
            var data = asm.GetResourceData(x);
            if (comp != null)
            {
                AddMemoryInternal(GetResourceUrl(urlRoot, name, start, p.Length, keep), location, data, mime.Item1, dur, null, lwt, etag, null, authTokens, replace, order);
                ext = p.Slice(ld2 + 1, ld - ld2 - 1);
                el = ext.Length;
                extl = el <= MaxStackExt ? stackalloc Char[el] : new Char[el];
                ext.ToLowerInvariant(extl);
                mime = MimeTypeMap.GetMimeType(extl);
                AddMemoryInternal(GetResourceUrl(urlRoot, name, start, ld, keep), location, data, mime.Item1, dur, (disableCompession || (!mime.Item2)) ? null : runtimeComp, lwt, etag, comp, authTokens, replace, order);
                return true;
            }
            AddMemoryInternal(GetResourceUrl(urlRoot, name, start, p.Length, keep), location, data, mime.Item1, dur, (disableCompession || (!mime.Item2)) ? null : runtimeComp, lwt, etag, null, authTokens, replace, order);
            return true;
        }

        /// <summary>
        /// The url of a resource name: the first <paramref name="length"/> chars of the name (starting at <paramref name="start"/>), where every '.' before <paramref name="keep"/> (relative to the start) becomes a '/' and the last "/min.js" becomes ".min.js", combined with the url root.
        /// The same as the split and joins of <see cref="AddEmbeddedResourceSplit"/> followed by PathFix, using one allocation.
        /// </summary>
        static String GetResourceUrl(String urlRoot, String name, int start, int length, int keep)
            => HttpServerTools.CombinePaths(urlRoot, String.Create(length, (name, start, keep), static (dst, s) =>
            {
                s.name.AsSpan(s.start, dst.Length).CopyTo(dst);
                dst.Slice(0, s.keep).Replace('.', '/');
                var i = dst.LastIndexOf("/min.js");
                if (i >= 0)
                    dst[i] = '.';
            }));

        /// <summary>
        /// Add an embedded resource (the original implementation, used for unusual names)
        /// </summary>
        void AddEmbeddedResourceSplit(Assembly asm, String x, String name, String sn, String urlRoot, int? clientCacheDuration, HttpCompressionPriority compression, bool disableCompession, DateTime lwt, String etag, String auth, bool replace, double order)
        {
            var pn = "Embedded resource \"";
            {
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
                    AddMemory(
                        HttpServerTools.CombinePaths(urlRoot, PathFix(String.Join('.', String.Join('/', s, 0, sl), s[sl], ext))),
                        location,
                        asm.GetResourceData(x),
                        mime.Item1, 
                        clientCacheDuration,
                        null,
                        true,
                        lwt,
                        etag,
                        null,
                        auth,
                        replace,
                        order);
                    ext = s[sl];
                    extl = ext.FastToLower();
                    mime = MimeTypeMap.GetMimeType(extl);
                }
                AddMemory(
                    HttpServerTools.CombinePaths(urlRoot, PathFix(String.Join('.', String.Join('/', s, 0, sl), ext))),
                    location,
                    asm.GetResourceData(x),
                    mime.Item1,
                    clientCacheDuration,
                    compression,
                    disableCompession || (!mime.Item2),
                    lwt,
                    etag,
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
        /// Serve some stream (typically from a resource).
        /// The stream is opened once to determine the length and then once per (uncached) request.
        /// </summary>
        /// <param name="url">The url to serve it from (relative to the module's url root)</param>
        /// <param name="location">A string that describes the location of this asset, filename on disc, embedded resource name etc</param>
        /// <param name="openStream">The function to use for opening a stream</param>
        /// <param name="mime">The mime to use</param>
        /// <param name="clientCacheDuration">The client side cache duration (null to use the modules default)</param>
        /// <param name="requestCacheDuration">The server side cache duration</param>
        /// <param name="compression">The runtime compression to apply (null to use the module default)</param>
        /// <param name="disableCompession">True to disable any compression</param>
        /// <param name="lastModified">When the resource was last modified (null to use the default = application start)</param>
        /// <param name="etag">The etag to use, defaults to using the lastModified time</param>
        /// <param name="preCompressedFormat">If the stream data is pre-compressed, the decoder of the compression format, else null</param>
        /// <param name="auth">Required authorization tokens, null = no auth required, "" = auth required but no specific tokens, or comma separated list of required security tokens</param>
        /// <param name="replace">If true, any existing resource will be replaced if it's of the same order</param>
        /// <param name="order">An optional order, if the same resource is added more than once, the one with the highest order (or if equal the last replaced) is used</param>
        /// <returns>True if added, false if an existing resource with a higher (or equal, when not replacing) order exists</returns>
        public bool AddStream(String url, String location, Func<Stream> openStream, String mime, int? clientCacheDuration = null, int requestCacheDuration = HttpServerTools.MaxRequestCache, HttpCompressionPriority compression = null, bool disableCompession = false, DateTime? lastModified = null, String etag = null, ICompDecoder preCompressedFormat = null, String auth = null, bool replace = false, double order = 0)
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
            url = HttpServerTools.CombinePaths(RootUri, url.Trim('/'));
            var d = new StaticStreamHttpRequestHandler(url, LocationPrefix + location, len, openStream, mime, compression, dur, requestCacheDuration, lastModified, etag, preCompressedFormat, authTokens, order);
            return AddHandler(url, d, replace);
        }

        /// <summary>
        /// Serve some memory.
        /// </summary>
        /// <param name="url">The url to serve it from (relative to the module's url root)</param>
        /// <param name="location">A string that describes the location of this asset, filename on disc, embedded resource name etc</param>
        /// <param name="data">The data to serve (not copied, must not be modified afterwards)</param>
        /// <param name="mime">The mime to use</param>
        /// <param name="clientCacheDuration">The client side cache duration (null to use the modules default)</param>
        /// <param name="compression">The runtime compression to apply (null to use the module default)</param>
        /// <param name="disableCompession">True to disable any compression</param>
        /// <param name="lastModified">When the resource was last modified (null to use the default = application start)</param>
        /// <param name="etag">The etag to use (null to derive it from the data and lastModified)</param>
        /// <param name="preCompressedFormat">If the data is pre-compressed, the decoder of the compression format, else null</param>
        /// <param name="auth">Required authorization tokens, null = no auth required, "" = auth required but no specific tokens, or comma separated list of required security tokens</param>
        /// <param name="replace">If true, any existing resource with the same order will be replaced</param>
        /// <param name="order">An optional order, if the same resource is added more than once, the one with the highest order (or if equal the last replaced) is used</param>
        /// <returns>True if added, false if an existing resource with a higher (or equal, when not replacing) order exists</returns>
        public bool AddMemory(String url, String location, ReadOnlyMemory<Byte> data, String mime, int? clientCacheDuration = null, HttpCompressionPriority compression = null, bool disableCompession = false, DateTime? lastModified = null, String etag = null, ICompDecoder preCompressedFormat = null, String auth = null, bool replace = false, double order = 0)
        {
            compression = disableCompession ? null : (compression ?? Compression);
            var dur = clientCacheDuration ?? ClientCacheDuration;
            var authTokens = auth == null ? null : Authorization.GetRequiredTokens(auth);
            if (location == null)
                location = GetLocation("Memory");
            return AddMemoryInternal(url, LocationPrefix + location, data, mime, dur, compression, lastModified, etag, preCompressedFormat, authTokens, replace, order);
        }

        /// <summary>
        /// The part of <see cref="AddMemory"/> after the arguments are resolved (the url is relative to the module's url root, the location includes the <see cref="LocationPrefix"/>)
        /// </summary>
        bool AddMemoryInternal(String url, String fullLocation, ReadOnlyMemory<Byte> data, String mime, int dur, HttpCompressionPriority compression, DateTime? lastModified, String etag, ICompDecoder preCompressedFormat, IReadOnlyList<String> authTokens, bool replace, double order)
        {
            url = HttpServerTools.CombinePaths(RootUri, url.Trim('/'));
            var d = new StaticMemoryHttpRequestHandler(url, fullLocation, data, mime, compression, dur, HttpServerTools.MaxRequestCache, lastModified, etag, preCompressedFormat, authTokens, order);
            return AddHandler(url, d, replace);
        }

        /// <summary>
        /// Serve some text.
        /// </summary>
        /// <param name="url">The url to serve it from (relative to the module's url root)</param>
        /// <param name="location">A string that describes the location of this asset, filename on disc, embedded resource name etc</param>
        /// <param name="text">The text to serve</param>
        /// <param name="mime">The mime to use</param>
        /// <param name="encoding">The text encoding to use (null to use default UTF8)</param>
        /// <param name="clientCacheDuration">The client side cache duration (null to use the modules default)</param>
        /// <param name="compression">The runtime compression to apply (null to use the module default)</param>
        /// <param name="disableCompession">True to disable any compression</param>
        /// <param name="lastModified">When the resource was last modified (null to use the default = application start)</param>
        /// <param name="etag">The etag to use (null to derive it from the data and lastModified)</param>
        /// <param name="auth">Required authorization tokens, null = no auth required, "" = auth required but no specific tokens, or comma separated list of required security tokens</param>
        /// <param name="replace">If true, any existing resource with the same order will be replaced</param>
        /// <param name="order">An optional order, if the same resource is added more than once, the one with the highest order (or if equal the last replaced) is used</param>
        /// <param name="storeCompressed">If true, the data is stored brotli compressed (best level) in memory and served pre-compressed</param>
        /// <returns>True if added, false if an existing resource with a higher (or equal, when not replacing) order exists</returns>
        public bool AddText(String url, String location, String text, String mime = MimeTypeMap.PlainText, Encoding encoding = null, int ? clientCacheDuration = null, HttpCompressionPriority compression = null, bool disableCompession = false, DateTime? lastModified = null, String etag = null, String auth = null, bool replace = false, double order = 0, bool storeCompressed = true)
        {
            compression = disableCompession ? null : (compression ?? Compression);
            var dur = clientCacheDuration ?? ClientCacheDuration;
            var e = encoding ?? Encoding.UTF8;
            ReadOnlyMemory<Byte> data;
            Byte[] rented = null;
            if (storeCompressed && !String.IsNullOrEmpty(text))
            {
                //  Only the compressed data is kept, so the encoded text is written to a pooled buffer
                rented = ArrayPool<Byte>.Shared.Rent(e.GetByteCount(text));
                data = new ReadOnlyMemory<Byte>(rented, 0, e.GetBytes(text, rented));
            }
            else
            {
                data = e.GetBytes(text);
            }
            var authTokens = auth == null ? null : Authorization.GetRequiredTokens(auth);
            if (location == null)
                location = GetLocation("String");
            ICompType comp = null;
            if (storeCompressed)
            {
                comp = Comp;
                try
                {
                    data = comp.GetCompressed(data.Span, CompEncoderLevels.Best);
                }
                finally
                {
                    if (rented != null)
                        ArrayPool<Byte>.Shared.Return(rented);
                }
            }
            url = HttpServerTools.CombinePaths(RootUri, url.Trim('/'));
            var d = new StaticMemoryHttpRequestHandler(url, LocationPrefix + location, data, mime, compression, dur, HttpServerTools.MaxRequestCache, lastModified, etag, comp, authTokens, order);
            return AddHandler(url, d, replace);
        }

        static readonly ICompType Comp = CompManager.GetFromHttp("br");

        readonly SemiFrozenDictionary<String, IStaticHttpRequestHandler> Handlers = new SemiFrozenDictionary<string, IStaticHttpRequestHandler>(StringComparer.Ordinal);

        /// <summary>
        /// Remove a resource.
        /// </summary>
        /// <param name="url">The full local url of the resource (including the module's url root)</param>
        /// <returns>True if removed</returns>
        public bool Remove(String url) => Handlers.TryRemove(url, out var d);

        /// <summary>
        /// Check if a resource is registered.
        /// </summary>
        /// <param name="url">The full local url of the resource (including the module's url root)</param>
        /// <returns>True if registered</returns>
        public bool Contains(String url) => Handlers.ContainsKey(url);

        /// <summary>
        /// Try to get a handler to a registered static resource
        /// </summary>
        /// <param name="localUrl">The local path to the resource</param>
        /// <returns>A handler for the resource or null if it doesn't exist</returns>
        public IStaticHttpRequestHandler TryGetHandler(String localUrl) => Handlers.TryGetValue(localUrl, out var d) ? d : null;

        /// <summary>
        /// Try to get a handler to a registered static resource, the same as <see cref="TryGetHandler(string)"/> without a string for the url (ex: an url built on the stack).
        /// </summary>
        /// <param name="localUrl">The local path to the resource</param>
        /// <returns>A handler for the resource or null if it doesn't exist</returns>
        public IStaticHttpRequestHandler TryGetHandler(ReadOnlySpan<Char> localUrl) => Handlers.TryGetValueSpan(localUrl, out var d) ? d : null;

        /// <summary>
        /// Returns the handler registered for the local url of a GET or HEAD request, else null.
        /// </summary>
        /// <param name="context">The request</param>
        /// <returns>The handler or null</returns>
        public IHttpRequestHandler Handler(HttpServerRequest context)
        {
            Handlers.TryGetValue(context.LocalUrl, out var handler);
            var m = context.HttpMethod;
            return ((m == HttpServerMethods.GET) || (m == HttpServerMethods.HEAD)) ? handler : null;
        }

        /// <inheritdoc/>
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
                //  The folder names are tested using spans (no string for an already seen folder)
                var folders = new HashSet<string>(StringComparer.Ordinal).GetAlternateLookup<ReadOnlySpan<Char>>();
                var lwt = HttpServerTools.StartedTime;
                var etag = HttpServerTools.StartedETag;
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
                        if (!folders.Add(url.AsSpan(ul, f - ul)))
                            continue;
                        yield return new HttpServerEndPoint(String.Concat(root, url.AsSpan(ul, f - ul)), ImplicitFolderLocation, lwt, etag);
                    }
                }
            }
        }


    }
}
