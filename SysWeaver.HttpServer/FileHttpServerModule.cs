using SysWeaver.Compression;

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SysWeaver.Net
{

    /// <summary>
    /// A transformer that can replace the handler of a file served by <see cref="FileHttpServerModule"/>, selected by the query string of the request
    /// (ex: "image.png?Thumb64x64" for a transformer registered with the key "Thumb64x64").
    /// Register using <see cref="FileHttpServerModule.AddFileTransformer(string, IFileTransformer)"/>.
    /// </summary>
    public interface IFileTransformer
    {
        /// <summary>
        /// Return a request handler for a given file.
        /// </summary>
        /// <param name="key">The key as registered (the query string of the request, without the '?')</param>
        /// <param name="mime">The mime type of the file and a flag that is true if the type is compressible</param>
        /// <param name="fi">File information (may be a pre-compressed variant of the requested file, see <paramref name="decoder"/>)</param>
        /// <param name="options">Request options of the disc folder that the file belongs to</param>
        /// <param name="isAccepted">True if the file isn't pre-compressed or if the pre-compressed format is accepted by the client</param>
        /// <param name="decoder">Non-null if the file is pre-compressed, else null</param>
        /// <param name="updateAccessTime">If true, the file's access time should be updated whenever the file is read</param>
        /// <param name="isDynamic">If true, the file is probably changed frequently</param>
        /// <returns>Must return a valid request handler</returns>
        /// <remarks>
        /// Called concurrently. The returned handler may be cached by the module for a few seconds.
        /// </remarks>
        Task<IHttpRequestHandler> Modify(String key, Tuple<String, bool> mime, FileInfo fi, RequestOptions options, bool isAccepted, ICompDecoder decoder, bool updateAccessTime, bool isDynamic);
    }

    /// <summary>
    /// A http server module that serves files from one or more disc folders, mapped to web folders (GET and HEAD requests only).
    /// </summary>
    /// <remarks>
    /// Several disc folders can be mapped to the same web folder, the most recently added folder is searched first.
    /// Longer (more specific) web folders are matched before shorter ones.
    /// Optionally serves pre-compressed variants (ex: "file.js.br") in place of the original file, and supports "virtual" files that only exist pre-compressed.
    /// Query strings can select an <see cref="IFileTransformer"/> (ex: thumbnails).
    /// Folders can be added and removed at runtime (thread safe), handler lookups are cached for a few seconds.
    /// </remarks>
    public sealed class FileHttpServerModule : IHttpServerModule, IPerfMonitored
    {

        delegate Task<IHttpRequestHandler> FtDel(String key, Tuple<String, bool> mime, FileInfo fi, RequestOptions options, bool isAccepted, ICompDecoder decoder, bool updateAccessTime, bool isDynamic);

        /// <summary>
        /// Create a file server module.
        /// </summary>
        /// <param name="p">Parameters (null to use defaults, i.e no folders)</param>
        /// <remarks>
        /// Folders whose disc folder doesn't exist are silently ignored.
        /// If <see cref="FileHttpServerModuleParams.CacheSeconds"/> is positive, handler lookups are cached (currently always for 5 seconds, regardless of the value).
        /// </remarks>
        public FileHttpServerModule(FileHttpServerModuleParams p = null)
        {
            p = p ?? new FileHttpServerModuleParams();
            PerfMon.Enabled = p.PerMon;
            var f = p.Folders;
            if (f != null)
            {
                foreach (var x in f)
                    AddFolder(x);
            }
            var c = p.CacheSeconds;
            if (c > 0)
            {
                Cache = new(TimeSpan.FromSeconds(5), StringComparer.Ordinal);
                AsyncHandler = InternalCachedHandler;
            }else
            {
                AsyncHandler = InternalUncachedHandler;
            }
        }

        /// <summary>
        /// Performance monitor of this module.
        /// </summary>
        public PerfMonitor PerfMon { get; private set; } = new PerfMonitor(nameof(FileHttpServerModule));


        /// <summary>
        /// Change the disc path of an already added disc folder (keeping all other options).
        /// </summary>
        /// <param name="webFolder">The web folder that the disc folder is mapped to (ex: "site/"), leading and trailing '/' are handled as in <see cref="AddFolder"/> ("" is the root web folder)</param>
        /// <param name="currentDiscFolder">The current full path of the disc folder (as stored, without a trailing separator)</param>
        /// <param name="newDiscFolder">The new full path (no trailing separator)</param>
        /// <returns>True if the folder was found and changed</returns>
        /// <remarks>
        /// The disc-to-web lookup used by <see cref="LocalToWeb(string)"/> is updated and the handler cache is cleared.
        /// </remarks>
        public bool ChangeDiscFolder(String webFolder, String currentDiscFolder, String newDiscFolder)
        {
            webFolder = (webFolder ?? "").Trim('/');
            if (webFolder.Length > 0)
                webFolder += '/';
            var r = WebFolders;
            lock (r)
            {
                if (!r.TryGetValue(webFolder, out var rs))
                    return false;
                var wt = rs.DiscFolders.FirstOrDefault(x => x.Path.FastEquals(currentDiscFolder));
                if (wt == null)
                    return false;
                wt.Path = newDiscFolder;
                UpdateFolderLookups(r);
            }
            return true;
        }

        /// <summary>
        /// Rebuild the ordered folders and the disc-to-web lookups from all web folders, and clear the handler cache.
        /// Must be called while holding the <see cref="WebFolders"/> lock.
        /// </summary>
        void UpdateFolderLookups(ConcurrentDictionary<String, WebFolder> r)
        {
            var ordered = r.OrderByDescending(x => x.Key.Length).ToArray();
            Dictionary<String, String> discToWeb = new(StringComparer.Ordinal);
            foreach (var x in ordered)
                foreach (var y in x.Value.DiscFolders)
                    discToWeb.TryAdd(y.Path + Path.DirectorySeparatorChar, x.Value.Url);
            OrderedFolders = ordered;
            DiscToWeb = discToWeb;
            DiscToWebPrefix = StringTree.Build(discToWeb.Keys);
            Cache?.Clear();
        }

        /// <summary>
        /// Add a folder (prefer to add folders using the constructor params).
        /// The added disc folder is searched before any previously added disc folders mapped to the same web folder.
        /// </summary>
        /// <param name="folder">The folder to add, a null <see cref="FileHttpServerModuleFolder.DiscFolder"/> means "web" (relative to the current directory)</param>
        /// <returns>True if the folder was added, false if the disc folder doesn't exist</returns>
        /// <remarks>Thread safe. Cached handler lookups are cleared.</remarks>
        public bool AddFolder(FileHttpServerModuleFolder folder)
        {
            var df = folder.DiscFolder ?? "web";
            var di = new DirectoryInfo(df);
            if (!di.Exists)
                return false;
            df = di.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var webFolder = (folder.WebFolder ?? "").Trim('/');
            if (webFolder.Length > 0)
                webFolder += '/';
            var r = WebFolders;
            lock (r)
            {
                if (!r.TryGetValue(webFolder, out var rs))
                {
                    rs = new WebFolder(webFolder);
                    r[webFolder] = rs;
                }
                rs.DiscFolders = rs.DiscFolders.PushFront(new DiscFolder(df, folder));
                UpdateFolderLookups(r);
            }
            return true;
        }

        Dictionary<String, String> DiscToWeb;
        StringTree DiscToWebPrefix;

        /// <summary>
        /// Remove a folder previously added (matched on the web folder and the full path of the disc folder).
        /// </summary>
        /// <param name="folder">The folder to remove</param>
        /// <returns>True if the folder was found and removed</returns>
        /// <remarks>Thread safe. Cached handler lookups are cleared.</remarks>
        public bool RemoveFolder(FileHttpServerModuleFolder folder)
        {
            if (folder == null)
                return false;
            var df = folder.DiscFolder ?? "web";
            var di = new DirectoryInfo(df);
            df = di.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var webFolder = (folder.WebFolder ?? "").Trim('/');
            if (webFolder.Length > 0)
                webFolder += '/';
            var r = WebFolders;
            lock (r)
            {
                if (!r.TryGetValue(webFolder, out var rs))
                    return false;
                var t = rs.DiscFolders;
                var i = t.IndexOf(x => x.Path.FastEquals(df));
                if (i < 0)
                    return false;
                t = t.RemoveAt(i);
                rs.DiscFolders = t;
                if (t.Length <= 0)
                    r.TryRemove(webFolder, out _);
                UpdateFolderLookups(r);
            }
            return true;
        }

        /// <summary>
        /// Map a local url (relative to the server root, no leading '/') to the full path of an existing file on disc.
        /// </summary>
        /// <param name="url">The local url, ex: "site/images/logo.png"</param>
        /// <returns>The full path of the first matching existing file, or null if no such file exists</returns>
        /// <remarks>
        /// Paths that resolve to a location outside of a disc folder (ex: ".." segments or rooted paths) are ignored.
        /// Pre-compressed variants are not considered.
        /// </remarks>
        public String WebToLocal(String url)
        {
            var f = OrderedFolders;
            if (f == null)
                return null;
            var toDiscPath = ToDiscPath;
            foreach (var webFolder in f)
            {
                var rootFolder = webFolder.Key;
                if (!url.StartsWith(rootFolder, StringComparison.Ordinal))
                    continue;
                var localDiscPath = toDiscPath(url.Substring(rootFolder.Length));
                foreach (var discFolder in webFolder.Value.DiscFolders)
                {
                    var absPath = SafeCombine(discFolder.Path, localDiscPath);
                    if ((absPath != null) && File.Exists(absPath))
                        return absPath;
                }

            }
            return null;
        }

        /// <summary>
        /// Map the full path of a file on disc to the local url that serves it.
        /// </summary>
        /// <param name="localFile">The full path of a file</param>
        /// <returns>The local url (no leading '/') or null if the file isn't inside any added disc folder</returns>
        public String LocalToWeb(String localFile)
        {
            var tree = DiscToWebPrefix;
            if (tree == null)
                return null;
            var p = tree.StartsWithAny(localFile);
            if (p == null)
                return null;
            var look = DiscToWeb;
            if (!look.TryGetValue(p, out var web))
                return null;
            var webName = web + localFile.Substring(p.Length).Replace('\\', '/'); ;
            return webName;

            /*foreach (var x in Repos)
            {
                var f = x.Value.Item1.ExposeFolder;
                if (f == null)
                    continue;
                var df = f.DiscFolder + Path.DirectorySeparatorChar;
                if (!localFile.StartsWith(df))
                    continue;
                return localFile.Substring(df.Length).Replace('\\', '/');
            }
            return null;
            */
        }


        const int ValidMethods = (1 << (int)HttpServerMethods.GET) | (1 << (int)HttpServerMethods.HEAD);

        /// <summary>
        /// Not used, this module always supplies an <see cref="AsyncHandler"/>.
        /// </summary>
        /// <param name="context">The request</param>
        /// <returns>Never returns</returns>
        /// <exception cref="NotImplementedException">Always thrown</exception>
        public IHttpRequestHandler Handler(HttpServerRequest context)
            => throw new NotImplementedException();

        readonly FastMemCache<String, IHttpRequestHandler> Cache;


        async Task<IHttpRequestHandler> InternalCachedHandler(HttpServerRequest context)
        {
            if (((ValidMethods >> (int)context.HttpMethod) & 1) == 0)
                return null;
            var url = context.LocalUrl;
            //  The handler depends on the file transformer (selected by the query string), so include the query in the key when it selects one (the local url never contains a '?')
            //  QueryStringStart is 0 when there is no query string
            var qs = context.QueryStringStart;
            var cacheKey = url;
            String ftKey = null;
            FtDel fileTransformer = null;
            if (qs > 0)
            {
                ftKey = context.Url.Substring(qs);
                if (FileTransformers.TryGetValue(ftKey, out fileTransformer))
                    cacheKey = String.Concat(url, "?", ftKey);
            }
            fileTransformer = fileTransformer ?? NoFT;
            return await Cache.GetOrUpdateAsync(cacheKey, async _ =>
            {
                var f = OrderedFolders;
                if (f == null)
                    return null;
                var toDiscPath = ToDiscPath;
                foreach (var webFolder in f)
                {
                    var rootFolder = webFolder.Key;
                    if (!url.FastStartsWith(rootFolder))
                        continue;
                    var localDiscPath = toDiscPath(url.Substring(rootFolder.Length));
                    foreach (var discFolder in webFolder.Value.DiscFolders)
                    {
                        var absPath = SafeCombine(discFolder.Path, localDiscPath);
                        if (absPath == null)
                            continue;
                        var fi = new FileInfo(absPath);
                        var ext = fi.Extension;
                        ICompDecoder decoder = null;
                        bool isAccepted = true;
                        if (discFolder.AssumePreCompressed)
                        {
                            var ti = GetSmallestPreComp(out decoder, out isAccepted, absPath, context.AcceptedEncoders, fi.Exists ? fi.LastWriteTimeUtc : null);
                            if ((ti != null) && ((!fi.Exists) || (ti.Length < fi.Length)))
                            {
                                fi = ti;
                            }
                            else
                            {
                                decoder = null;
                                isAccepted = ti == null;
                            }
                        }
                        if (!fi.Exists)
                            continue;
                        var mime = MimeTypeMap.GetMimeType(ext.FastToLower());
                        return await fileTransformer(ftKey, mime, fi, discFolder, isAccepted, decoder, discFolder.UpdateAccessTime, discFolder.IsDynamic).ConfigureAwait(false);
                    }
                }
                return null;
            }).ConfigureAwait(false);
        }

        async Task<IHttpRequestHandler> InternalUncachedHandler(HttpServerRequest context)
        {
            if (((ValidMethods >> (int)context.HttpMethod) & 1) == 0)
                return null;
            var url = context.LocalUrl;
            var f = OrderedFolders;
            if (f == null)
                return null;
            var toDiscPath = ToDiscPath;
            foreach (var webFolder in f)
            {
                var rootFolder = webFolder.Key;
                if (!url.FastStartsWith(rootFolder))
                    continue;
                var localDiscPath = toDiscPath(url.Substring(rootFolder.Length));
                foreach (var discFolder in webFolder.Value.DiscFolders)
                {
                    var absPath = SafeCombine(discFolder.Path, localDiscPath);
                    if (absPath == null)
                        continue;
                    var fi = new FileInfo(absPath);
                    var ext = fi.Extension;
                    ICompDecoder decoder = null;
                    bool isAccepted = true;
                    if (discFolder.AssumePreCompressed)
                    {
                        var ti = GetSmallestPreComp(out decoder, out isAccepted, absPath, context.AcceptedEncoders, fi.Exists ? fi.LastWriteTimeUtc : null);
                        if ((ti != null) && ((!fi.Exists) || (ti.Length < fi.Length)))
                        {
                            fi = ti;
                        }
                        else
                        {
                            decoder = null;
                            isAccepted = ti == null;
                        }
                    }
                    if (!fi.Exists)
                        continue;
                    var mime = MimeTypeMap.GetMimeType(ext.FastToLower());
                    //  QueryStringStart is 0 when there is no query string
                    var ftKey = context.QueryStringStart > 0 ? context.Url.Substring(context.QueryStringStart) : null;
                    FtDel fileTransformer = null;
                    if (ftKey != null)
                        FileTransformers.TryGetValue(ftKey, out fileTransformer);
                    fileTransformer = fileTransformer ?? NoFT;
                    return await fileTransformer(ftKey, mime, fi, discFolder, isAccepted, decoder, discFolder.UpdateAccessTime, discFolder.IsDynamic).ConfigureAwait(false);
                }
            }
            return null;
        }

        /// <summary>
        /// The handler lookup (always set): returns a handler for GET/HEAD requests whose local url maps to an existing file (or pre-compressed variant), else null.
        /// The query string (if any) selects a registered <see cref="IFileTransformer"/>.
        /// </summary>
        public Func<HttpServerRequest, Task<IHttpRequestHandler>> AsyncHandler { get; init; }



        #region File transforms


        static readonly FtDel NoFT = (fileTransform, mime, fi, discFolder, isAccepted, decoder, updateAccessTime, isDynamic) => Task.FromResult((IHttpRequestHandler)new FileHttpRequestHandler(mime, fi, discFolder, isAccepted, decoder, updateAccessTime, isDynamic));

        /// <summary>
        /// Register a file transformer, used when a file is requested with a query string that equals the <paramref name="suffix"/>.
        /// </summary>
        /// <param name="suffix">The query string (without the '?') that selects the transformer, case sensitive</param>
        /// <param name="t">The transformer</param>
        /// <returns>True if added, false if a transformer is already registered for the suffix</returns>
        /// <exception cref="ArgumentNullException"><paramref name="t"/> is null (debug builds only; release builds throw a <see cref="NullReferenceException"/>)</exception>
        public bool AddFileTransformer(String suffix, IFileTransformer t)
        {
#if DEBUG
            ArgumentNullException.ThrowIfNull(t);
#endif//DEBUG
            return FileTransformers.TryAdd(suffix, t.Modify);
        }

        /// <summary>
        /// Remove a file transformer.
        /// </summary>
        /// <param name="suffix">The query string that the transformer was registered with</param>
        /// <returns>True if removed</returns>
        public bool RemoveFileTransformer(String suffix) => FileTransformers.TryRemove(suffix, out var t);

        readonly ConcurrentDictionary<String, FtDel> FileTransformers = new ConcurrentDictionary<string, FtDel>(StringComparer.Ordinal);

        #endregion//File transforms


        readonly ConcurrentDictionary<String, WebFolder> WebFolders = new(StringComparer.Ordinal);
        

        KeyValuePair<String, WebFolder>[] OrderedFolders;


        public override string ToString() => String.Concat(
            nameof(WebFolders), ": ", WebFolders.Count);

        #region Helpers

        static Func<String, String> GetToDiscPath()
        {
            var c = Path.DirectorySeparatorChar;
            if (c == '/')
                return x => x;
            return x => x.Replace('/', c);
        }

        static Func<String, String> GetToUrlPath()
        {
            var c = Path.DirectorySeparatorChar;
            if (c == '/')
                return x => x;
            return x => x.Replace(c, '/');
        }


        static readonly Func<String, String> ToDiscPath = GetToDiscPath();

        /// <summary>
        /// Chars that are never allowed in the (url decoded) relative disc path: NUL, and on Windows ':' (drive letters and alternate data streams).
        /// </summary>
        static readonly SearchValues<Char> InvalidLocalPathChars = SearchValues.Create(OperatingSystem.IsWindows() ? "\0:" : "\0");

        /// <summary>
        /// Comparison used for file system paths (case insensitive on Windows).
        /// </summary>
        static readonly StringComparison PathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        /// <summary>
        /// Combine a disc folder with a relative path that comes from a (url decoded, untrusted) request url.
        /// </summary>
        /// <param name="discFolder">The full path of the disc folder (no trailing separator)</param>
        /// <param name="localDiscPath">The relative path</param>
        /// <returns>The full path, or null if the path contains invalid chars or resolves to a location outside of the disc folder (ex: ".." segments or rooted paths)</returns>
        static String SafeCombine(String discFolder, String localDiscPath)
        {
            if (localDiscPath.AsSpan().ContainsAny(InvalidLocalPathChars))
                return null;
            var absPath = Path.GetFullPath(Path.Combine(discFolder, localDiscPath));
            var l = discFolder.Length;
            if ((absPath.Length <= l) || (absPath[l] != Path.DirectorySeparatorChar) || !absPath.StartsWith(discFolder, PathComparison))
                return null;
            return absPath;
        }

        static readonly Func<String, String> ToUrlPath = GetToUrlPath();

        static String GetWebPreCompressed(String ext)
        {
            var decomp = CompManager.GetFromExt(ext.FastToLower());
            if (decomp == null)
                return null;
            var code = decomp.HttpCode;
            return code.Length > 0 ? code : null;
        }

        static String RemoveExt(String url)
        {
            var l = url.LastIndexOf('.');
            return l < 0 ? url : url.Substring(0, l);
        }

        static String GetExt(String url)
        {
            var l = url.LastIndexOf('.');
            return l < 0 ? "" : url.Substring(l);
        }

        #endregion Helpers



        /// <summary>
        /// Given an uncompressed file, returns the smallest valid pre-compressed file (if any), preferring formats accepted by the client.
        /// </summary>
        /// <param name="decoder">Output of the decoder with the smallest size</param>
        /// <param name="isAccepted">True if the decoder is among the accepted encoders (typically meaning that there is no runtime decompression / compression)</param>
        /// <param name="absPath">The path to the uncompressed file</param>
        /// <param name="acceptedEncoders">A set of the accepted compression coders (typically from the client via the http accept encodings header)</param>
        /// <param name="orgTime">If the compressed data may not be older than a time (typically the original file), specify it here</param>
        /// <returns>File information about the pre-compressed alternative or null if non exist</returns>
        static FileInfo GetSmallestPreComp(out ICompDecoder decoder, out bool isAccepted, String absPath, IReadOnlySet<String> acceptedEncoders, DateTime? orgTime = null)
        {
            decoder = null;
            long smallest = long.MaxValue;
            FileInfo fis = null;
            isAccepted = true;
            if (acceptedEncoders != null)
            {
                // TODO: Use Directory.GetFiles instead? Cache? Faster?

                //  Find smallest of the accepted encoders
                foreach (var x in CompManager.ExtensionHandlers)
                {
                    if (x.Key[0] == '.')
                        continue;
                    var dec = x.Value;
                    if (!acceptedEncoders.Contains(dec.HttpCode))
                        continue;
                    var fi = new FileInfo(String.Join('.', absPath, x.Key));
                    if (!fi.Exists)
                        continue;
                    var l = fi.Length;
                    if (l >= smallest)
                        continue;
                    if ((orgTime != null) && (fi.LastWriteTimeUtc < orgTime))
                        continue;
                    decoder = dec;
                    smallest = l;
                    fis = fi;
                }
            }
            if (fis == null)
            {
                isAccepted = false;
                //  Find smallest using any encoder
                foreach (var x in CompManager.ExtensionHandlers)
                {
                    if (x.Key[0] == '.')
                        continue;
                    var dec = x.Value;
                    var fi = new FileInfo(String.Join('.', absPath, x.Key));
                    if (!fi.Exists)
                        continue;
                    var l = fi.Length;
                    if (l >= smallest)
                        continue;
                    if ((orgTime != null) && (fi.LastWriteTimeUtc < orgTime))
                        continue;
                    decoder = dec;
                    smallest = l;
                    fis = fi;
                }
            }
            return fis;
        }

        readonly ConcurrentDictionary<String, Tuple<DateTime, TextTemplate, bool>> Templates = new ();

        /*
        TextTemplate GetTextTemplate(out bool isDynamic, FileInfo fi, ICompDecoder decoder, HttpServerRequest context)
        {
            var ts = Templates;
            var fn = fi.FullName;
            ts.TryGetValue(fn, out var t);
            var ft = fi.LastWriteTimeUtc;
            if ((t == null) || (t.Item1 != ft))
            {
                using (PerfMon.Track("CreateTemplate"))
                {
                    String text;
                    if (decoder == null)
                        text = FileExt.ReadText(fn);
                    else
                    {
                        Memory<Byte> b;
                        using (var s = fi.OpenRead())
                            b = decoder.GetDecompressed(s);
                        text = Encoding.UTF8.GetString(b.Span);
                    }
                    var temp = new TextTemplate(text);
                    isDynamic = context.Server.IsDynamic(temp);
                    t = Tuple.Create(ft, temp, isDynamic);
                }
                ts[fn] = t;
            }
            isDynamic = t.Item3;
            return t.Item2;
        }
        */

        /// <summary>
        /// Get the enpoint for a given file (and a virtual enpoint if applicable)
        /// </summary>
        /// <param name="fileEp">Enpoint information for a file</param>
        /// <param name="virtualFileEp">Additional virtual endpoint information (if applicable)</param>
        /// <param name="absPath">The path to an existing file</param>
        /// <param name="seen">A table of already seen url's (to avoid duplicates)</param>
        /// <param name="df">The disc folder that this file belong to</param>
        /// <param name="epl">The length of the base path (that should be removed from the url)</param>
        /// <param name="uriPrefix">A prefix to add to the uri</param>
        void GetEndPointFromFile(out HttpServerEndPoint fileEp, out HttpServerEndPoint virtualFileEp, String absPath, HashSet<String> seen, DiscFolder df, int epl, String uriPrefix)
        {
            fileEp = null;
            virtualFileEp = null;
            try
            {
                var url = uriPrefix + ToUrlPath(absPath.Substring(epl));
                if (!seen.Add(url))
                    return;
                var comp = df.Compression?.ToString();
                var fi = new FileInfo(absPath);
                var ext = fi.Extension.FastToLower();
                var mime = MimeTypeMap.GetMimeType(ext);
                if (!df.AssumePreCompressed)
                {
                    var loc = String.Join(fi.FullName, "[File] \"", '"');
                    var len = fi.Length;
                    var lwt = fi.LastWriteTimeUtc;
                    fileEp = new HttpServerEndPoint
                    (
                        url,
                        "GET",
                        df.ClientCacheDuration,
                        len < df.MaxCacheSize ? df.RequestCacheDuration : 0,
                        mime.Item2 ? comp : null,
                        null,
                        df.Auth,
                        HttpServerEndpointTypes.File,
                        loc,
                        len,
                        lwt,
                        HttpServerTools.ToEtag(lwt),
                        mime.Item1,
                        null
                    );
                    return;
                }else
                {
                    var ci = GetSmallestPreComp(out var decompC, out var acc, absPath, ReadOnlyData.Set(CompManager.HttpCodes), fi.LastWriteTimeUtc) ?? fi;
                    var loc = String.Join(ci.FullName, "[File] \"", '"');
                    var len = fi.Length;
                    var lwt = fi.LastWriteTimeUtc;
                    fileEp = new HttpServerEndPoint
                    (
                        url,
                        "GET",
                        df.ClientCacheDuration,
                        len < df.MaxCacheSize ? df.RequestCacheDuration : 0,
                        mime.Item2 ? comp : null,
                        decompC?.HttpCode,
                        df.Auth,
                        HttpServerEndpointTypes.File,
                        loc,
                        len,
                        lwt,
                        HttpServerTools.ToEtag(lwt),
                        mime.Item1,
                        null
                    );
                }
                //  If this is a compressed file, we might add it as a virtual file
                var decomp = GetWebPreCompressed(ext);
                if (decomp == null)
                    return;
                var org = RemoveExt(url);
                if (!seen.Add(org))
                    return;
                var orgDisc = RemoveExt(absPath);
                if (File.Exists(orgDisc))
                    return;
                {
                    fi = GetSmallestPreComp(out var decompC, out var acc, orgDisc, ReadOnlyData.Set(CompManager.HttpCodes)) ?? fi;
                    if (decompC != null)
                        decomp = decompC.HttpCode;
                    var loc = String.Join(fi.FullName, "[Virtual File] \"", '"');
                    var len = fi.Length;
                    var lwt = fi.LastWriteTimeUtc;
                    ext = GetExt(org).FastToLower();
                    mime = MimeTypeMap.GetMimeType(ext);
                    virtualFileEp = new HttpServerEndPoint
                    (
                        org,
                        "GET",
                        df.ClientCacheDuration,
                        len < df.MaxCacheSize ? df.RequestCacheDuration : 0,
                        mime.Item2 ? comp : null,
                        decomp,
                        df.Auth,
                        HttpServerEndpointTypes.File,
                        loc,
                        len,
                        lwt,
                        HttpServerTools.ToEtag(lwt),
                        mime.Item1,
                        null
                    );
                }
            }
            catch
            {
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Scans the disc, enumerating with a null <paramref name="root"/> recursively scans all folders (slow).
        /// </remarks>
        public IEnumerable<IHttpServerEndPoint> EnumEndPoints(String root = null)
        {
            IEnumerable<KeyValuePair<String, WebFolder>> wfs = OrderedFolders;
            var uriPrefix = root ?? String.Empty;
            if (wfs != null)
            {
                String[] emptyArray = [];
                var opt = new EnumerationOptions();
                if (root == null)
                {
                //  To enumerate it all (slow, not recommended)
                    opt.RecurseSubdirectories = true;
                    root = "";
                }
                else
                {
                    //  Find what web folders to get files from
                    var lwt = HttpServerTools.StartedTime;
                    var etag = HttpServerTools.StartedETag;
                    List<KeyValuePair<String, WebFolder>> folders = new List<KeyValuePair<string, WebFolder>>();
                    var rootParts = root.Length <= 0 ? emptyArray : root.TrimEnd('/').Split('/');
                    var rl = rootParts.Length;
                    foreach (var wf in wfs)
                    {
                        var webRoot = wf.Key;
                        var urlPaths = webRoot.Length <= 0 ? emptyArray : webRoot.TrimEnd('/').Split('/');
                        var ul = urlPaths.Length;
                        if (ul >= rl)
                        {
                            bool ok = true;
                            for (int i = 0; i < rl; ++i)
                            {
                                ok = rootParts[i] == urlPaths[i];
                                if (!ok)
                                    break;
                            }
                            if (ok)
                            {
                                if (ul > rl)
                                {
                                    yield return new HttpServerEndPoint(String.Join('/', urlPaths, 0, rl + 1), "[Virtual Folder] from [File System]", lwt, etag);
                                }
                                else
                                {
                                    folders.Add(wf);
                                }
                            }
                        }else
                        {
                            bool ok = true;
                            for (int i = 0; i < ul; ++i)
                            {
                                ok = rootParts[i] == urlPaths[i];
                                if (!ok)
                                    break;
                            }
                            if (ok)
                                folders.Add(wf);
                        }
                    }
                    wfs = folders;
                }
                var toDiscPath = ToDiscPath;
                var toUrlPath = ToUrlPath;
                HashSet<String> seen = new();
            //  Iterate over web folders
                foreach (var wf in wfs)
                {
                    var w = wf.Value;
                    String baseFolder = "";
                    if (root.StartsWith(w.Url, StringComparison.Ordinal))
                    {
                        baseFolder = toDiscPath(root.Substring(w.Url.Length)).Trim(Path.DirectorySeparatorChar);
                        if (baseFolder.Length > 0)
                            baseFolder = Path.DirectorySeparatorChar + baseFolder;
                        baseFolder += Path.DirectorySeparatorChar;
                    }
                //  Iterate over disc folders
                    foreach (var df in w.DiscFolders)
                    {
                        var ep = df.Path + baseFolder;
                        if (!Directory.Exists(ep))
                            continue;
                        var epl = ep.Length;
                        String[] files;
                    //  Add folders (if we're not enumerating recursively, since then we only want actual end points)
                        if (!opt.RecurseSubdirectories)
                        {
                            try
                            {
                                files = Directory.GetDirectories(ep, "*", opt);
                            }
                            catch
                            {
                                files = emptyArray;
                            }
                            foreach (var f in files)
                            {
                                HttpServerEndPoint eps = null;
                                try
                                {
                                    var url = uriPrefix + toUrlPath(f.Substring(epl));
                                    if (!seen.Add(url))
                                        continue;
                                    var di = new DirectoryInfo(f);
                                    var loc = String.Join(di.FullName, "[Folder] \"", '"');
                                    var lwt = di.LastWriteTimeUtc;
                                    eps = new HttpServerEndPoint(url, loc, lwt, HttpServerTools.ToEtag(lwt));
                                }
                                catch
                                {
                                }
                                if (eps != null)
                                    yield return eps;
                            }
                        }
                        try
                        {
                            files = Directory.GetFiles(ep, "*", opt);
                        }
                        catch
                        {
                            continue;
                        }
                    //  Add files
                        foreach (var f in files)
                        {
                            GetEndPointFromFile(out var fep, out var vfep, f, seen, df, epl, uriPrefix);
                            if (vfep != null)
                                yield return vfep;
                            if (fep != null)
                                yield return fep;
                        }
                    }
                }
            }
        }
    }


}
