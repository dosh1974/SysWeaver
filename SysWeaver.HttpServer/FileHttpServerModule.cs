using SysWeaver.Compression;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SysWeaver.Net
{

    public interface IFileTransformer
    {
        /// <summary>
        /// Return a request handler for a given file.
        /// </summary>
        /// <param name="key">The key as registered</param>
        /// <param name="mime">The mime information</param>
        /// <param name="fi">File information</param>
        /// <param name="options">Request options</param>
        /// <param name="isAccepted">True if the file is pre-compressed and a compressed copy is acceptable</param>
        /// <param name="decoder">Non-null if the file is pre-compressed, else null</param>
        /// <param name="updateAccessTime">If true, the file's access time is updated whenever the file is read</param>
        /// <returns>Must return a valid request handler</returns>
        Task<IHttpRequestHandler> Modify(String key, Tuple<String, bool> mime, FileInfo fi, RequestOptions options, bool isAccepted, ICompDecoder decoder, bool updateAccessTime);
    }

    /// <summary>
    /// A http server module that serves files from disc
    /// </summary>
    public sealed class FileHttpServerModule : IHttpServerModule, IPerfMonitored
    {

        delegate Task<IHttpRequestHandler> FtDel(String key, Tuple<String, bool> mime, FileInfo fi, RequestOptions options, bool isAccepted, ICompDecoder decoder, bool updateAccessTime);

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

        public PerfMonitor PerfMon { get; private set; } = new PerfMonitor(nameof(FileHttpServerModule));


        /// <summary>
        /// Add a folder (prefer to add folders using the constructor params)
        /// </summary>
        /// <param name="folder">The folder to add</param>
        /// <returns></returns>
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
                rs.DiscFolders.Add(new DiscFolder(df, folder));
                OrderedFolders = r.OrderByDescending(x => x.Key.Length).ToArray();




                Dictionary<String, String> discToWeb = new Dictionary<string, string>(StringComparer.Ordinal);
                HashSet<String> seen = new HashSet<string>(StringComparer.Ordinal);
                var tree = StringTree.Build(r.SelectMany(x => x.Value.DiscFolders.Select(a => a.Path + Path.DirectorySeparatorChar).Where(b => seen.Add(b))));
                foreach (var x in r.Values)
                    foreach (var y in x.DiscFolders)
                        discToWeb[y.Path + Path.DirectorySeparatorChar] = x.Url;
                DiscToWeb = discToWeb;
                DiscToWebPrefix = tree;
            }
            return true;
        }

        Dictionary<String, String> DiscToWeb;
        StringTree DiscToWebPrefix;

        public bool RemoveFolder(FileHttpServerModuleFolder folder)
        {
            return true;
        }

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
                    var absPath = Path.Combine(discFolder.Path, localDiscPath);
                    if (File.Exists(absPath))
                        return absPath;
                }

            }
            return null;
        }

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

        public IHttpRequestHandler Handler(HttpServerRequest context)
            => throw new NotImplementedException();

        readonly FastMemCache<String, IHttpRequestHandler> Cache;

        static readonly ValueTask<IHttpRequestHandler> NoHandler = ValueTask.FromResult((IHttpRequestHandler)null);

        ValueTask<IHttpRequestHandler> InternalCachedHandler(HttpServerRequest context)
        {
            if (((ValidMethods >> (int)context.HttpMethod) & 1) == 0)
                return NoHandler;
            var url = context.LocalUrl;
            return Cache.GetOrUpdateValueAsync(url, async _ =>
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
                        var absPath = Path.Combine(discFolder.Path, localDiscPath);
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
                        var ftKey = context.Url.Substring(context.QueryStringStart);
                        FileTransformers.TryGetValue(ftKey, out var fileTransformer);
                        fileTransformer = fileTransformer ?? NoFT;
                        return await fileTransformer(ftKey, mime, fi, discFolder, isAccepted, decoder, discFolder.UpdateAccessTime).ConfigureAwait(false);
                    }
                }
                return null;
            });
        }

        async ValueTask<IHttpRequestHandler> InternalUncachedHandler(HttpServerRequest context)
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
                    var absPath = Path.Combine(discFolder.Path, localDiscPath);
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
                    var ftKey = context.Url.Substring(context.QueryStringStart);
                    FileTransformers.TryGetValue(ftKey, out var fileTransformer);
                    fileTransformer = fileTransformer ?? NoFT;
                    return await fileTransformer(ftKey, mime, fi, discFolder, isAccepted, decoder, discFolder.UpdateAccessTime).ConfigureAwait(false);
                }
            }
            return null;
        }

        /// <summary>
        /// An optional async handler
        /// </summary>
        public Func<HttpServerRequest, ValueTask<IHttpRequestHandler>> AsyncHandler { get; init; }



        #region File transforms


        static readonly FtDel NoFT = (fileTransform, mime, fi, discFolder, isAccepted, decoder, updateAccessTime) => Task.FromResult((IHttpRequestHandler)new FileHttpRequestHandler(mime, fi, discFolder, isAccepted, decoder, updateAccessTime));

        public bool AddFileTransformer(String suffix, IFileTransformer t) => FileTransformers.TryAdd(suffix, (t ?? throw new ArgumentNullException(nameof(t))).Modify);

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
        /// Given an uncompressed file, returns the smallest valied pre-compressed file (if any)
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
                        text = File.ReadAllText(fn);
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
                        true,
                        mime.Item2 ? comp : null,
                        null,
                        df.Auth,
                        HttpServerEndpointTypes.File,
                        loc,
                        len,
                        lwt,
                        mime.Item1,
                        null
                    );
                    return;
                }else
                {
                    var ci = GetSmallestPreComp(out var decompC, out var acc, absPath, CompManager.HttpCodeHandlers.KeysAsReadOnlySet(), fi.LastWriteTimeUtc) ?? fi;
                    var loc = String.Join(ci.FullName, "[File] \"", '"');
                    var len = fi.Length;
                    var lwt = fi.LastWriteTimeUtc;
                    fileEp = new HttpServerEndPoint
                    (
                        url,
                        "GET",
                        df.ClientCacheDuration,
                        len < df.MaxCacheSize ? df.RequestCacheDuration : 0,
                        true,
                        mime.Item2 ? comp : null,
                        decompC?.HttpCode,
                        df.Auth,
                        HttpServerEndpointTypes.File,
                        loc,
                        len,
                        lwt,
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
                    fi = GetSmallestPreComp(out var decompC, out var acc, orgDisc, CompManager.HttpCodeHandlers.KeysAsReadOnlySet()) ?? fi;
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
                        true,
                        mime.Item2 ? comp : null,
                        decomp,
                        df.Auth,
                        HttpServerEndpointTypes.File,
                        loc,
                        len,
                        lwt,
                        mime.Item1,
                        null
                    );
                }
            }
            catch
            {
            }
        }

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
                                    yield return new HttpServerEndPoint(String.Join('/', urlPaths, 0, rl + 1), "[Virtual Folder] from [File System]", HttpServerTools.StartedTime);
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
                                    eps = new HttpServerEndPoint(url, loc, di.LastWriteTimeUtc);
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
