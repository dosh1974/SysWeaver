using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SysWeaver.Auth;
using SysWeaver.Data;
using SysWeaver.Compression;
using SysWeaver.Net;
using System.Threading;
using SysWeaver.Serialization;
using System.Buffers;
using System.Collections.Concurrent;

namespace SysWeaver.MicroService
{


    /// <summary>
    /// Can be used to store user owned files with support for:
    /// - File distribution on multiple physical discs (load balancing).
    /// - Access scope (private, protected, public etc).
    /// - Files of suitable types are compressed on disc (and in transit).
    /// - Old data is automatically pruned (files are deleted if the time since last accees exceeds the policy).
    /// - Disc quota is maintained (the files that would expire in the neareast future is deleted when exceeded).
    /// </summary>
    [WebApiUrl("UserStorage")]
    public sealed class UserStorageService : IUserStorageService, IHttpServerModule, IDisposable, IPerfMonitored, IHaveStats
    {
        public UserStorageService(ServiceManager manager, UserStorageParams p)
        {
            var fu = manager.TryGet<FileUploaderService>();
            FileUploader = fu;

            p = p ?? new UserStorageParams();
            var folders = p.Folders;
            var fl = folders?.Length ?? 0;
            if (fl <= 0)
            {
                folders = Folders.Append(Folders.AllAppFolders, "UserStorage");
                fl = 1;
            }
            else
            {
                for (int i = 0; i < fl; ++i)
                {
                    var f = Path.GetFullPath(PathTemplate.Resolve(folders[i]));
                    PathExt.EnsureFolderExist(f);
                    folders[i] = f;
                }
            }
            BaseUrl = p.BaseUrl ?? "storage";
            BaseUrlPrefix = BaseUrl.Length <= 0 ? "" : (BaseUrl + '/');
            if (BaseUrlPrefix.Length > 0)
                OnlyForPrefixes = [BaseUrlPrefix];
            Paths = folders;
            if (!String.IsNullOrEmpty(p.Comp))
            {
                Comp = CompManager.GetFromHttp(p.Comp);
                CompExt = "." + (Comp.FileExtensions.FirstOrDefault() ?? Comp.HttpCode).TrimStart('.');
                Level = p.Level;
            }
            Ser = SerManager.Get(String.IsNullOrEmpty(p.Ser) ? "json" : p.Ser);
            Opt = new RequestOptions(30, 5, 1 << 20, "deflate:Balanced,gzip:Balanced", null);

            var t = typeof(UserStorageService);
            var td = t.Assembly.GetUncompressedResourceData(t.Namespace + ".data.embed.html");
            var et = Encoding.UTF8.GetString(td.Span);
            EmbedComp = HttpCompressionPriority.GetSupportedEncoders("br:Balanced,deflate:Balanced,gzip:Balanced");
            EmbeddTemplate = new TextTemplate(et, "${", "}");
            PerUserHandler = manager.TryGet<IUserStoragePerUserHandler>();
            Retention = p.Retention ?? new UserStorageDataRetention();
        //  Add file repos
            if (fu != null)
            {
                var u = p.UploadPublic;
                if (u != null)
                {
                    var r = new UserStorageFileRepo("UserPublic", u, (c, n) => GetUploadPath(c, n, true, null), GetMaxSize, GetUploadDiscFile);
                    Repos.Add(r);
                    fu.AddRepo(r);
                }
                u = p.UploadProtected;
                if (u != null)
                {
                    var r = new UserStorageFileRepo("UserProtected", u, (c, n) => GetUploadPath(c, n, true, ""), GetMaxSize, GetUploadDiscFile);
                    Repos.Add(r);
                    fu.AddRepo(r);
                }
                u = p.UploadPrivate;
                if (u != null)
                {
                    var r = new UserStorageFileRepo("UserPrivate", u, (c, n) => GetUploadPath(c, n, false, null), GetMaxSize, GetUploadDiscFile);
                    Repos.Add(r);
                    fu.AddRepo(r);
                }
            }
            PruneTask = new PeriodicTask(Prune, 15000);
        }


        public String[] OnlyForPrefixes { get; init; }


        readonly List<UserStorageFileRepo> Repos = new List<UserStorageFileRepo>();




        readonly FileUploaderService FileUploader;

        public Task<String> GetBaseUrlPrefix() => Task.FromResult(BaseUrlPrefix);


        readonly HttpCompressionPriority EmbedComp;

        readonly TextTemplate EmbeddTemplate;

        public override string ToString() =>
            String.IsNullOrEmpty(BaseUrl) ?
                String.Join("; ", Paths.Select(x => x.ToQuoted()))
                :
                String.Join(" @ ", BaseUrlPrefix, String.Join("; ", Paths.Select(x => x.ToQuoted())));

        int PrunePos;

        const String Base64SpecialChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_+";

        readonly ExceptionTracker DeleteExceptions = new ExceptionTracker();
        readonly ISerializerType Ser;

        bool DeleteFileAndEmptyPath(String file, int stopAtPathLen, bool isLink)
        {
            using var perf = PerfMon.Track(nameof(DeleteFileAndEmptyPath));
            try
            {
                File.Delete(file);
                for (; ; )
                {
                    file = Path.GetDirectoryName(file);
                    if (file.Length <= stopAtPathLen)
                        return true;
                    if (Directory.GetDirectories(file, "*").Length > 0)
                        return true;
                    if (Directory.GetFiles(file, "*").Length > 0)
                        return true;
                    Directory.Delete(file);
                }
            }
            catch (Exception ex)
            {
                DeleteExceptions.OnException(ex);
                return false;
            }
        }

        long CalcDiscSize(int shardIndex, long fileSize)
        {
            //  TODO actually get from shard path?
            const long sectorSize = 4096;
            const long dirEntrySize = 4096;
            var size = (fileSize + sectorSize - 1) & ~(sectorSize - 1);
            size += dirEntrySize * 2;
            return size;
        }


        async ValueTask<long> GetMaxSize(string userGuid)
        {
            var puh = PerUserHandler;
            var defRetention = Retention;
            var retention = defRetention;
            if (puh != null)
                retention = (await puh.GetUserDataRetention(userGuid).ConfigureAwait(false)) ?? retention;
            return retention.GetMaxDiscBytes(defRetention.DiscQuotaMb);
        }

        async ValueTask<bool> Prune()
        {
            using var perf = PerfMon.Track(nameof(Prune));
            StoredFileCache.Prune();
            var paths = Paths;
            var pathCount = paths.Length;
            var chars = Base64SpecialChars;
            var puh = PerUserHandler;
            var now = DateTime.UtcNow;
            var defRetention = Retention;
            for (int i = 0; i < 32; ++i)
            {
                var pi = Interlocked.Increment(ref PrunePos) & 63;
                var key = chars[pi & 0x3f];
                HashSet<String> users = new HashSet<string>(StringComparer.Ordinal);
                foreach (var path in paths)
                {
                    var pl = path.Length + 1;
                    var dirs = Directory.GetDirectories(path, key.ToString() + '*');
                    foreach (var y in dirs)
                    {
                        if (y[pl] == key)
                            users.Add(Path.GetFileName(y));
                    }
                }
                if (users.Count <= 0)
                    continue;
                foreach (var userPath in users)
                {
                    var retention = defRetention;
                    if (puh != null)
                    {
                        var userGuid = UserPathToGuid(userPath);
                        retention = (await puh.GetUserDataRetention(userGuid).ConfigureAwait(false)) ?? retention;
                    }
                    var times = retention.Get(defRetention);
                    var maxDiscBytes = retention.GetMaxDiscBytes(defRetention.DiscQuotaMb);
                    List<Tuple<DateTime, String, long, int, bool>> userFiles = new();
                    long sum = 0;
                    for (int pathIndex = 0; pathIndex < pathCount; ++pathIndex)
                    {
                        var path = paths[pathIndex];
                        var folder = Path.Combine(path, userPath);
                        if (!Directory.Exists(folder))
                            continue;
                        var maxRemove = path.Length + 1;
                        var pl = folder.Length + 1;
                        foreach (var filename in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
                        {
                            var local = filename.Substring(pl);
                            var parts = local.Split(Path.DirectorySeparatorChar);
                            if (parts.Length != 3)
                            {
                                DeleteFileAndEmptyPath(filename, maxRemove, false);
                                continue;
                            }
                            //  Get retention time from scope
                            var scope = parts[0];
                            var time = times[(int)UserStorageScopes.Private];
                            if (!scope.FastEquals("private"))
                            {
                                time = times[(int)UserStorageScopes.Protected];
                                if (scope.FastEquals("public"))
                                    time = times[(int)UserStorageScopes.Public];
                            }
                            //  Check if it's time to delete
                            var fi = new FileInfo(filename);
                            if (!fi.Exists)
                                continue;
                            var rng = parts[1];
                            bool isLink = rng.Length == 1 && rng.FastEquals("l");
                            var exp = fi.LastAccessTimeUtc.Add(time);
                            if (now < exp)
                            {
                                var size = CalcDiscSize(pathIndex, fi.Length);
                                sum += size;
                                userFiles.Add(Tuple.Create(exp, filename, size, maxRemove, isLink));
                                continue;
                            }
                            //  Delete file
                            DeleteFileAndEmptyPath(filename, maxRemove, isLink);
                        }
                    }
                    if (sum <= maxDiscBytes)
                        continue;
                    //  Need to remove files
                    userFiles.Sort((a, b) => a.Item1.CompareTo(b.Item1));
                    foreach (var x in userFiles)
                    {
                        DeleteFileAndEmptyPath(x.Item2, x.Item4, x.Item5);
                        sum -= x.Item3;
                        if (sum <= maxDiscBytes)
                            break;
                    }
                }
                break;
            }
            return true;
        }

        PeriodicTask PruneTask;

        public void Dispose()
        {
            Interlocked.Exchange(ref PruneTask, null)?.Dispose();
            var p = FileUploader;
            if (p != null)
            {
                foreach (var x in Repos)
                    p.RemoveRepo(x);
            }
        }

        readonly IUserStoragePerUserHandler PerUserHandler;
        readonly UserStorageDataRetention Retention;
        readonly ICompType Comp;
        readonly String CompExt;

        readonly IReadOnlyDictionary<String, ICompType> CompTypes = CompManager.ExtensionHandlers.Select(x => new KeyValuePair<String, ICompType>("." + x.Key.TrimStart('.'), CompManager.GetFromHttp(x.Value.HttpCode))).WithUniqueKeys(StringComparer.Ordinal).ToDictionary(StringComparer.Ordinal).Freeze();


        readonly CompEncoderLevels Level;
        readonly String BaseUrl;
        readonly String BaseUrlPrefix;
        readonly String[] Paths;
        readonly RequestOptions Opt;


        String ToSafeString(Span<Char> buffer, int size)
        {
            for (int i = 0; i < size; ++i)
            {
                var t = buffer[i];
                if (t == '/')
                    t = '_';
                if (t == '=')
                    t = '-';
                if (t == '+')
                    t = '.';
                buffer[i] = t;
            }
            return new String(buffer.Slice(0, size));
        }

        static void WriteSafeString(Span<char> to, String from)
        {
            var size = to.Length;
            for (int i = 0; i < size; ++i)
            {
                var t = from[i];
                if (t == '/')
                    t = '_';
                if (t == '=')
                    t = '-';
                if (t == '+')
                    t = '.';
                to[i] = t;
            }
        }

        static readonly SpanAction<char, String> WriteSafeStringAction = WriteSafeString;


        String ToSafeString(String x)
            => String.Create(x.Length, x, WriteSafeStringAction);

        void FromSafeString(Span<Char> dest, ReadOnlySpan<Char> src, int size)
        {
            for (int i = 0; i < size; ++i)
            {
                var t = src[i];
                if (t == '_')
                    t = '/';
                if (t == '-')
                    t = '=';
                if (t == '.')
                    t = '+';
                dest[i] = t;
            }
        }

        public String GetUserPath(String userGuid)
        {
            var data = Encoding.UTF8.GetBytes(userGuid);
            var dl = data.Length;
            var size = ((dl << 2) + 2 / 3) + 8;
            Span<Char> bc = stackalloc char[size];
            if (!Convert.TryToBase64Chars(data.AsSpan(), bc, out var ww))
                throw new Exception("Internal error");
            return ToSafeString(bc, ww);
        }

        String UserPathToGuid(String userPath)
        {
            var size = userPath.Length;
            Span<Char> t = stackalloc char[size];
            FromSafeString(t, userPath.AsSpan(), size);
            Span<Byte> dest = stackalloc Byte[size];
            if (!Convert.TryFromBase64Chars(t, dest, out size))
                throw new Exception("Internal error");
            return Encoding.UTF8.GetString(dest.Slice(0, size));
        }

        String GetBasePath(bool isPublic, HttpServerRequest context, String requireAuthTokens = null)
        {
            var user = GetUserPath(context.Session.Auth?.Guid);
            String scope = "private";
            if (isPublic)
            {
                scope = "public";
                var tokens = Authorization.GetRequiredTokens(requireAuthTokens);
                if (tokens != null)
                {
                    scope = "protected";
                    if (tokens.Count > 0)
                        scope = "_" + String.Join("_", tokens.OrderBy(x => x));
                }
            }
            return String.Join("/", user, scope);
        }


        ValueTuple<String, String> GetUploadPath(HttpServerRequest context, String filename, bool isPublic, String auth)
        {
            var bp = GetBasePath(isPublic, context, auth);
            var r = new Random((int)QuickHash.Hash(filename));
            var folders = Paths;
            int shard = r.Next(folders.Length);
            bp = String.Join('/', bp, 'u');
            var path = Path.Combine(folders[shard], bp);
            PathExt.EnsureFolderExist(path);
            filename = PathExt.SafeFilename(filename);
            var url = String.Join("/", BaseUrl, bp, shard, filename);
            return ValueTuple.Create(Path.Combine(path, filename), url);
        }

        String GetRandomPath(out int shard, int shardCount, bool userDeletable, String metaData)
        {
            Span<Byte> bytes = stackalloc Byte[18];
            using (var r = SecureRng.Get())
            {
                shard = (int)r.GetUInt32Max((uint)shardCount);
                if (metaData != null)
                    return metaData;
                r.GetBytes(bytes);
            }
            Span<Char> bc = stackalloc char[25];
            if (!Convert.TryToBase64Chars(bytes, bc, out var ww))
                throw new Exception("Internal error");
            UserStorageFileFlags flags = userDeletable ? UserStorageFileFlags.UserDeletable : 0;
            bc[ww] = (Char)('a' + flags);
            return ToSafeString(bc, ww + 1);
        }

        /// <summary>
        /// Store some private data, only accessible by the user who stored it
        /// </summary>
        /// <param name="context">The request context that triggered this store</param>
        /// <param name="filename">The desired filename and extension (no path)</param>
        /// <param name="data">The data to store</param>
        /// <param name="userDeletable">If true, the user can delete this file</param>
        /// <returns>The url to the file</returns>
        public Task<String> StorePrivateFile(HttpServerRequest context, String filename, ReadOnlyMemory<Byte> data, bool userDeletable = true)
            => InternalStoreFile(filename, data, GetBasePath(false, context), userDeletable);

        /// <summary>
        /// Store some public data, accessible to anyone that have the specified auth
        /// </summary>
        /// <param name="context">The request context that triggered this store</param>
        /// <param name="filename">The desired filename and extension (no path)</param>
        /// <param name="data">The data to store</param>
        /// <param name="requireAuthTokens">null = Anyone can access the data, even if they are not logged in.
        /// "" = Any logged in user can access the data.
        /// One or more tokens that is accepted to read the data.
        /// </param>
        /// <param name="userDeletable">If true, the user can delete this file</param>
        /// <returns>The url to the file</returns>
        public Task<String> StorePublicFile(HttpServerRequest context, String filename, ReadOnlyMemory<Byte> data, String requireAuthTokens = null, bool userDeletable = true)
            => InternalStoreFile(filename, data, GetBasePath(true, context, requireAuthTokens), userDeletable);


        /// <summary>
        /// Store a link to something as private, only accessible by the user who stored it
        /// </summary>
        /// <param name="context">The request context that triggered this store</param>
        /// <param name="url">The link (url) to store</param>
        /// <param name="storedFiles">The url's to any stored files that are required to display the link correctly, they should not be user deletable</param>
        /// <returns>The url that can be used to view the stored link</returns>
        public Task<string> StorePrivateLink(HttpServerRequest context, string url, params String[] storedFiles)
            => InternalStoreLink(context, url, GetBasePath(false, context), storedFiles);

        /// <summary>
        /// Store a link to something as public or protected, only accessible by the user who stored it
        /// </summary>
        /// <param name="context">The request context that triggered this store</param>
        /// <param name="url">The link (url) to store</param>
        /// <param name="requireAuthTokens">
        /// null or "-" = Anyone can access the data, even if they are not logged in.
        /// "" = Any logged in user can access the data.
        /// One or more tokens that is accepted to read the data.
        /// </param>
        /// <param name="storedFiles">The url's to any stored files that are required to display the link correctly (must be stored with the same auth), they should not be user deletable</param>
        /// <returns>The url that can be used to view the stored link</returns>
        public Task<string> StorePublicLink(HttpServerRequest context, string url, string requireAuthTokens, params String[] storedFiles)
            => InternalStoreLink(context, url, GetBasePath(true, context, requireAuthTokens), storedFiles);

        async Task<String> InternalStoreLink(HttpServerRequest context, string url, string basePath, String[] storedFiles)
        {
            var link = new UserStorageLink
            {
                Url = url,
                Files = storedFiles
            };
            var ser = Ser;
            var data = ser.Serialize(link);
            Byte[] bytes;
            using (var r = SecureRng.Get())
                bytes = r.GetBytes(18);
            var bc = Convert.ToBase64String(bytes);
            var filename = String.Join(".", ToSafeString(bc), ser.Extension);
            filename = await InternalStoreFile(filename, data, basePath, false, "l").ConfigureAwait(false);
            return filename.Substring(0, filename.Length - ser.Extension.Length - 1);
        }


        async Task<String> InternalStoreFile(String filename, ReadOnlyMemory<Byte> data, String bp, bool userDeletable, String metaData = null)
        {
            using var _ = PerfMon.Track(nameof(InternalStoreFile));
            var folders = Paths;
            bp = String.Join("/", bp, GetRandomPath(out var shard, folders.Length, userDeletable, metaData));
            var path = Path.Combine(folders[shard], bp);
            PathExt.EnsureFolderExist(path);
            filename = PathExt.SafeFilename(filename);
            var t = MimeTypeMap.GetMimeType(Path.GetExtension(filename));
            var url = String.Join("/", BaseUrl, bp, shard, filename);
            var cmp = Comp;
            if (t.Item2 && (cmp != null))
            {
                data = cmp.GetCompressed(data.Span, Level);
                filename += CompExt;
            }
            var filePath = Path.Combine(path, filename);
            await data.WriteToFileAsync(filePath).ConfigureAwait(false);
            return url;
        }


        /// <summary>
        /// Read a file from the store
        /// </summary>
        /// <param name="context">The request context that triggered this read</param>
        /// <param name="url">Url to the file</param>
        /// <param name="markAsAccessed">If true the file is marked as accessed and expiration time is moved forward</param>
        /// <returns>The uncompressed content of the file</returns>
        public async Task<ReadOnlyMemory<Byte>?> ReadFile(HttpServerRequest context, string url, bool markAsAccessed = true)
        {
            if (!Validate(out var fi, out var mime, out var compType, out var isLink, out var scope, context, url, markAsAccessed))
                return null;
            ReadOnlyMemory<Byte> data = await File.ReadAllBytesAsync(fi.FullName).ConfigureAwait(false);
            if (compType != null)
                data = compType.GetDecompressed(data.Span);
            return data;                
        }


        /// <summary>
        /// Get the scope of a link or a file
        /// </summary>
        /// <param name="context"></param>
        /// <param name="url">Url to the file or link</param>
        /// <returns>The scope</returns>
        public Task<UserStorageScopes?> GetScope(HttpServerRequest context, string url)
        {
            UserStorageScopes? sc = null;
            if (Validate(out var fi, out var mime, out var compType, out var isLink, out var scope, context, url, false))
                sc = scope;
            return Task.FromResult(sc);
        }


        ValueTuple<FileInfo, ICompType> GetUploadDiscFile(String filename)
        {
            GetDiscFile(out var ext, out var comp, out var fi, filename);
            return ValueTuple.Create(fi, comp);
        }

        bool GetDiscFile(out String ext, out ICompType comp, out FileInfo fi, String filename)
        {
            fi = new FileInfo(filename);
            ext = fi.Extension;
            comp = null;
            if (fi.Exists)
                return true;
            foreach (var x in CompTypes)
            {
                fi = new FileInfo(filename + x.Key);
                if (fi.Exists)
                {
                    comp = x.Value;
                    return true;
                }
            }
            return false;
        }

        bool Validate(out FileInfo fi, out Tuple<String, bool> mime, out ICompType compType, out bool isLink, out UserStorageScopes scope, HttpServerRequest context, string l, bool markAsAccessed)
        {
            fi = null;
            mime = null;
            compType = null;
            isLink = false;
            scope = UserStorageScopes.Public;
            var bp = BaseUrlPrefix;
            int r = 0;
            if (bp.Length > 0)
            {
                if (!l.FastStartsWith(bp))
                    return false;
                r = 1;
            }
            var parts = l.Split('/');
            /*
                "storage" (optional) see bp
                "U0k6N21ldGFlN204c2dkYmdqNzN5dm5ucDVrb2E-"
                "private"
                "d7NeNdZE9w02uDgOitzBYX9R"
                "0"
                "Happy%20Mushroom%20Jumping.png"
            */
            if (parts.Length != (5 + r))
                return false;
            if (!uint.TryParse(parts[3 + r], out var shardIndex))
                return false;
            var paths = Paths;
            if (shardIndex >= (uint)paths.Length)
                return false;
            var userPath = parts[0 + r];
            var scopeS = parts[1 + r];
            var rngFlags = parts[2 + r];
            var filename = parts[4 + r];
            var fname = Path.Combine(
                paths[shardIndex],
                userPath,
                scopeS,
                rngFlags,
                filename);
            isLink = rngFlags.FastEquals("l");
            if (isLink)
                fname = String.Join(".", fname, Ser.Extension);
            if (!GetDiscFile(out var ext, out compType, out fi, fname))
                return false;
            if (scopeS != "public")
            {
                scope = UserStorageScopes.Protected;
                var session = context.Session;
                var a = session?.Auth;
                if (a == null)
                    throw new NoUserLoggedInException();
                if (scopeS == "private")
                {
                    scope = UserStorageScopes.Private;
                    if (GetUserPath(a.Guid) != userPath)
                        if (!session.IsValid(ForceAllow))
                            throw new UserNotAllowedException();
                }
                else
                {
                    if (scopeS != "protected")
                    {
                        var tokens = scopeS.Split('_', StringSplitOptions.RemoveEmptyEntries);
                        if (!a.IsValid(tokens))
                            if (!session.IsValid(ForceAllow))
                                throw new UserNotAllowedException();
                    }
                }
            }
            mime = MimeTypeMap.GetMimeType((ext ?? fi.Extension).FastToLower());
            if (markAsAccessed)
                fi.LastAccessTimeUtc = DateTime.UtcNow;
            return true;
        }


        public IHttpRequestHandler Handler(HttpServerRequest context)
        {
            var l = context.LocalUrl;
            if (!Validate(out var fi, out var mime, out var compType, out var isLink, out var scope, context, l, false))
                return null;
            var qs = context.QueryStringStart;
            if ((qs <= 0) || (!context.Url.Substring(qs).FastEquals("peak")))
                fi.LastAccessTimeUtc = DateTime.UtcNow;
            //  Validated everything, return file data
            if (isLink)
            {
                var linkInfo = InternalLoadLink(fi.FullName);
                var baseUrl = "../../../../../";
                var newUrl = baseUrl + linkInfo.Url;
                var res = EmbeddTemplate.Get(x => x.FastEquals("BaseUrl") ? baseUrl : newUrl);
                var mem = Encoding.UTF8.GetBytes(res);
                return new StaticMemoryHttpRequestHandler(l, "Embedded", mem, HttpServerTools.HtmlMime, EmbedComp, 30, 60, HttpServerTools.ToEtag(fi.LastAccessTimeUtc));
                //context.SetResStatusCode(302);
                //context.SetResHeader("Location", newUrl);
                //return HttpServerTools.AlreadyHandled;
            }
            return new FileHttpRequestHandler(mime, fi, Opt, true, compType);
        }

        public IEnumerable<IHttpServerEndPoint> EnumEndPoints(string root = null) => HttpServerTools.NoEndPoints;


        static readonly IReadOnlyList<String> ForceAllow = new String[]
        {
            "debug",
            "admin",
        };


        #region StoredFile


        /// <summary>
        /// Read files from disc
        /// </summary>
        /// <param name="userGuid">Guid of the user</param>
        /// <returns>The files and the the user path</returns>
        async Task<Tuple<IReadOnlyList<StoredFile>, String>> FetchStoredFiles(String userGuid)
        {
            using var _ = PerfMon.Track(nameof(FetchStoredFiles));
            userGuid = userGuid.Split('\n')[0];
            List<StoredFile> files = new List<StoredFile>();
            var defRetention = Retention;
            var retention = defRetention;
            var puh = PerUserHandler;
            if (puh != null)
                retention = (await puh.GetUserDataRetention(userGuid).ConfigureAwait(false)) ?? retention;
            var times = retention.Get(defRetention);
            var paths = Paths;
            var pl = paths.Length;
            var userPath = GetUserPath(userGuid);
            for (int i = 0; i < pl; ++i)
            {
                var pc = Path.Combine(paths[i], userPath);
                if (!Directory.Exists(pc))
                    continue;
                var discFiles = Directory.GetFiles(pc, "*", SearchOption.AllDirectories);
                var pcl = pc.Length + 1;
                var dl = discFiles.Length;
                for (int di = 0; di < dl; ++di)
                {
                    var fi = new FileInfo(discFiles[di]);
                    if (!fi.Exists)
                        continue;
                    var parts = fi.FullName.Substring(pcl).Split(Path.DirectorySeparatorChar);
                    if (parts.Length != 3)
                        continue;
                    var r = parts[1];
                    if (r.Length == 1)
                        if (r.FastEquals("l"))
                            continue;
                    var scope = parts[0];
                    var isPrivate = scope.FastEquals("private");
                    String auth = "";
                    var sc = UserStorageScopes.Private;
                    if (!isPrivate)
                    {
                        sc = UserStorageScopes.Protected;
                        if (scope.FastEquals("public"))
                        {
                            sc = UserStorageScopes.Public;
                            auth = null;
                        }
                        else
                        {
                            if (!scope.FastEquals("protected"))
                            {
                                if (scope[0] != '_')
                                    continue;
                                var tokens = scope.Split('_', StringSplitOptions.RemoveEmptyEntries);
                                auth = String.Join(',', tokens);
                            }
                        }
                    }
                    var filename = parts[2];
                    var ext = Path.GetExtension(filename).FastToLower();
                    if (CompTypes.TryGetValue(ext, out var comp))
                        filename = filename.Substring(0, filename.Length - ext.Length);
                    var url = BaseUrlPrefix + String.Join('/', userPath, scope, parts[1], i, filename);
                    var la = fi.LastAccessTimeUtc;
                    var exp = la.Add(times[(int)sc]);
                    files.Add(new StoredFile(url, CalcDiscSize(i, fi.Length), comp?.HttpCode, isPrivate, auth, fi.LastWriteTimeUtc, la, exp, i));
                }

            }
            return new Tuple<IReadOnlyList<StoredFile>, String>(files, userPath);
        }

        /// <summary>
        /// Delete a stored file
        /// </summary>
        /// <param name="url">The url of the file</param>
        /// <param name="context"></param>
        /// <returns>True if file was deleted</returns>
        [WebApi]
        [WebApiAuth]
        public bool DeleteStoredFile(String url, HttpServerRequest context)
            => LinkedDeleteStoredFile(url, context);
        
        bool LinkedDeleteStoredFile(String url, HttpServerRequest context, bool isLinked = false)
        {
            using var _ = PerfMon.Track(nameof(DeleteStoredFile));
            var session = context.Session;
            var a = session?.Auth;
            if (a == null)
                throw new NoUserLoggedInException();
            var bp = BaseUrlPrefix;
            int r = 0;
            if (bp.Length > 0)
            {
                if (!url.FastStartsWith(bp))
                    return false;
                r = 1;
            }
            var parts = url.Split('/');
            if (parts.Length != (5 + r))
                return false;
            var userPath = parts[0 + r];
            if (userPath != GetUserPath(a.Guid))
            {
                if (!context.Session.IsValid(ForceAllow))
                    throw new UserNotAllowedException();
            }
            if (!uint.TryParse(parts[3 + r], out var shardIndex))
                return false;
            var paths = Paths;
            if (shardIndex >= (uint)paths.Length)
                return false;
            var randomGuid = parts[2 + r];
            if (!isLinked)
            {
                UserStorageFileFlags flags = (UserStorageFileFlags)(randomGuid[randomGuid.Length - 1] - 'a');
                if (randomGuid.FastEquals("u"))
                    flags = UserStorageFileFlags.UserDeletable;
                if ((flags & UserStorageFileFlags.UserDeletable) == 0)
                    throw new UserNotAllowedException();
            }
            var pathShard = paths[shardIndex];
            var scope = parts[1 + r];
            var fname = Path.Combine(
                pathShard,
                userPath,
                scope,
                randomGuid,
                parts[4 + r]);
            if (!GetDiscFile(out var ext, out var compType, out var fi, fname))
                return false;
            DeleteFileAndEmptyPath(fi.FullName, pathShard.Length, false);
            if (!isLinked)
                context.Session.InvalidateCache();
            return true;
        }


        /// <summary>
        /// Delete any stored file, no auth checking!
        /// </summary>
        /// <param name="url">The url of the file</param>
        /// <returns>True if file was deleted</returns>
        public bool InternalDeleteStoredFile(String url)
        {
            using var _ = PerfMon.Track(nameof(InternalDeleteStoredFile));
            var bp = BaseUrlPrefix;
            int r = 0;
            if (bp.Length > 0)
            {
                if (!url.FastStartsWith(bp))
                    return false;
                r = 1;
            }
            var parts = url.Split('/');
            if (parts.Length != (5 + r))
                return false;
            var userPath = parts[0 + r];
            if (!uint.TryParse(parts[3 + r], out var shardIndex))
                return false;
            var paths = Paths;
            if (shardIndex >= (uint)paths.Length)
                return false;
            var scope = parts[1 + r];
            var pathShard = paths[shardIndex];
            var fname = Path.Combine(
                pathShard,
                userPath,
                scope,
                parts[2 + r],
                parts[4 + r]);
            if (!GetDiscFile(out var ext, out var compType, out var fi, fname))
                return false;
            DeleteFileAndEmptyPath(fi.FullName, pathShard.Length, false);
            return true;
        }


        /// <summary>
        /// Get all stored files
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        /// <exception cref="NoUserLoggedInException"></exception>
        [WebApi]
        [WebApiAuth]
        public async Task<StoredFile[]> GetStoredFiles(HttpServerRequest context)
            => (await InternalGetStoredFiles(context).ConfigureAwait(false)).Item1.ToArray();


        /// <summary>
        /// Get all stored files for the logged in user
        /// </summary>
        /// <param name="context"></param>
        /// <returns>The files and the the user path</returns>
        /// <exception cref="NoUserLoggedInException"></exception>
        public Task<Tuple<IReadOnlyList<StoredFile>, String>> InternalGetStoredFiles(HttpServerRequest context)
        {
            using var _ = PerfMon.Track(nameof(InternalGetStoredFiles));
            var session = context.Session;
            var a = session.Auth;
            if (a == null)
                throw new NoUserLoggedInException();
            var key = String.Join('\n', a.Guid, session.CacheTimeStamp);
            return StoredFileCache.GetOrUpdateAsync(key, FetchStoredFiles);
        }

        /// <summary>
        /// Get all files stored by the logged in user
        /// </summary>
        /// <param name="r">Paramaters</param>
        /// <param name="context"></param>
        /// <returns>Table data with the files</returns>
        [WebApi]
        [WebApiAuth]
        public async Task<TableData> MyStoredFiles(TableDataRequest r, HttpServerRequest context)
        {
            var sf = await InternalGetStoredFiles(context).ConfigureAwait(false);
            var files = sf.Item1;
            var userPath = sf.Item2;
            var root = String.Join(userPath, BaseUrlPrefix, '/');
            var cl = root.Length;
            var res = TableDataTools.Get(r, 15000, files.Select(x => new StoredFileAction(x, cl)));
            var cols = res.Cols;
            if (cols != null)
            {
                cols[StoredFileUrlIndex].Format = new TableDataUrlAttribute("{0}", String.Join(root, "../", "{0}")).Value;
                res.Title = "Stored files";
            }
            return res;
        }

        static readonly int StoredFileUrlIndex = TableDataTools.GetColumnIndex<StoredFileAction>(nameof(StoredFileAction.Url));

        readonly FastMemCache<string, Tuple<IReadOnlyList<StoredFile>, String>> StoredFileCache = new FastMemCache<string, Tuple<IReadOnlyList<StoredFile>, String>>(TimeSpan.FromSeconds(14), StringComparer.Ordinal);

        #endregion // StoredFile


        #region StoredUrl


        /// <summary>
        /// Read urls from disc
        /// </summary>
        /// <param name="userGuid">Guid of the user</param>
        /// <returns>The urls and the the user path</returns>
        async Task<Tuple<IReadOnlyList<StoredUrl>, String>> FetchStoredUrls(String userGuid)
        {
            using var _ = PerfMon.Track(nameof(FetchStoredUrls));
            userGuid = userGuid.Split('\n')[0];
            List<StoredUrl> urls = new List<StoredUrl>();
            var defRetention = Retention;
            var retention = defRetention;
            var puh = PerUserHandler;
            if (puh != null)
                retention = (await puh.GetUserDataRetention(userGuid).ConfigureAwait(false)) ?? retention;
            var times = retention.Get(defRetention);
            var paths = Paths;
            var pl = paths.Length;
            var compExt = CompExt;
            var compExtL = compExt.Length;
            var userPath = GetUserPath(userGuid);
            for (int i = 0; i < pl; ++i)
            {
                var pc = Path.Combine(paths[i], userPath);
                if (!Directory.Exists(pc))
                    continue;
                var discUrls = Directory.GetFiles(pc, "*", SearchOption.AllDirectories);
                var pcl = pc.Length + 1;
                var dl = discUrls.Length;
                for (int di = 0; di < dl; ++di)
                {
                    var fi = new FileInfo(discUrls[di]);
                    if (!fi.Exists)
                        continue;
                    var parts = fi.FullName.Substring(pcl).Split(Path.DirectorySeparatorChar);
                    if (parts.Length != 3)
                        continue;
                    var r = parts[1];
                    if (r.Length != 1)
                        continue;
                    if (!r.FastEquals("l"))
                        continue;
                    var scope = parts[0];
                    var isPrivate = scope.FastEquals("private");
                    String auth = "";
                    var sc = UserStorageScopes.Private;
                    if (!isPrivate)
                    {
                        sc = UserStorageScopes.Protected;
                        if (scope.FastEquals("public"))
                        {
                            sc = UserStorageScopes.Public;
                            auth = null;
                        }
                        else
                        {
                            if (!scope.FastEquals("protected"))
                            {
                                if (scope[0] != '_')
                                    continue;
                                var tokens = scope.Split('_', StringSplitOptions.RemoveEmptyEntries);
                                auth = String.Join(',', tokens);
                            }
                        }
                    }
                    var urlname = parts[2];
                    var isCompressed = (compExt != null) && urlname.EndsWith(compExt);
                    if (isCompressed)
                        urlname = urlname.Substring(0, urlname.Length - compExtL);
                    var url = BaseUrlPrefix + String.Join('/', userPath, scope, parts[1], i, urlname.Substring(0, urlname.Length - 1 - Ser.Extension.Length));
                    var la = fi.LastAccessTimeUtc;
                    var exp = la.Add(times[(int)sc]);
                    urls.Add(new StoredUrl(url, CalcDiscSize(i, fi.Length), isCompressed, isPrivate, auth, fi.LastWriteTimeUtc, la, exp, i));
                }

            }
            return new Tuple<IReadOnlyList<StoredUrl>, String>(urls, userPath);
        }


        /// <summary>
        /// Delete a stored url
        /// </summary>
        /// <param name="url">The url</param>
        /// <param name="context"></param>
        /// <returns>True if url was deleted</returns>
        [WebApi]
        [WebApiAuth]
        public bool DeleteStoredUrl(String url, HttpServerRequest context)
        {
            using var _ = PerfMon.Track(nameof(DeleteStoredUrl));
            var session = context.Session;
            var a = session?.Auth;
            if (a == null)
                throw new NoUserLoggedInException();
            var bp = BaseUrlPrefix;
            int r = 0;
            if (bp.Length > 0)
            {
                if (!url.FastStartsWith(bp))
                    return false;
                r = 1;
            }
            var parts = url.Split('/');
            if (parts.Length != (5 + r))
                return false;
            var userPath = parts[0 + r];
            if (userPath != GetUserPath(a.Guid))
            {
                if (!context.Session.IsValid(ForceAllow))
                    throw new UserNotAllowedException();
            }
            if (!uint.TryParse(parts[3 + r], out var shardIndex))
                return false;
            var paths = Paths;
            if (shardIndex >= (uint)paths.Length)
                return false;
            var randomGuid = parts[2 + r];
            if (!randomGuid.FastEquals("l"))
                throw new UserNotAllowedException();
            var scope = parts[1 + r];
            var pathShard = paths[shardIndex];
            var fname = Path.Combine(
                pathShard,
                userPath,
                scope,
                randomGuid,
                String.Join(".", parts[4 + r], Ser.Extension));
            var fi = new FileInfo(fname);
            if (!fi.Exists)
            {
                var ce = CompExt;
                if (ce == null)
                    return false;
                fi = new FileInfo(fname + ce);
                if (!fi.Exists)
                    return false;
            }
            var link = InternalLoadLink(fi.FullName);
            var linkedFiles = link.Files;
            if (linkedFiles != null)
            {
                foreach (var x in link.Files)
                    LinkedDeleteStoredFile(x, context, true);
            }
            DeleteFileAndEmptyPath(fi.FullName, pathShard.Length, true);
            context.Session.InvalidateCache();
            return true;
        }

        /// <summary>
        /// Get all stored urls
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        /// <exception cref="NoUserLoggedInException"></exception>
        [WebApi]
        [WebApiAuth]
        public async Task<StoredUrl[]> GetStoredUrls(HttpServerRequest context)
            => (await InternalGetStoredUrls(context).ConfigureAwait(false)).Item1.ToArray();


        /// <summary>
        /// Get all stored urls for the logged in user
        /// </summary>
        /// <param name="context"></param>
        /// <returns>The urls and the the user path</returns>
        /// <exception cref="NoUserLoggedInException"></exception>
        public Task<Tuple<IReadOnlyList<StoredUrl>, String>> InternalGetStoredUrls(HttpServerRequest context)
        {
            using var _ = PerfMon.Track(nameof(InternalGetStoredUrls));
            var session = context.Session;
            var a = session.Auth;
            if (a == null)
                throw new NoUserLoggedInException();
            var key = String.Join('\n', a.Guid, session.CacheTimeStamp);
            return StoredUrlCache.GetOrUpdateAsync(key, FetchStoredUrls);
        }

        /// <summary>
        /// Get all urls stored by the logged in user
        /// </summary>
        /// <param name="r">Paramaters</param>
        /// <param name="context"></param>
        /// <returns>Table data with the urls</returns>
        [WebApi]
        [WebApiAuth]
        public async Task<TableData> MyStoredUrls(TableDataRequest r, HttpServerRequest context)
        {
            var sf = await InternalGetStoredUrls(context).ConfigureAwait(false);
            var urls = sf.Item1;
            var userPath = sf.Item2;
            var root = String.Join(userPath, BaseUrlPrefix, '/');
            var cl = root.Length;
            var res = TableDataTools.Get(r, 15000, urls.Select(x => new StoredUrlAction(x, cl)));
            var cols = res.Cols;
            if (cols != null)
            {
                cols[StoredUrlUrlIndex].Format = new TableDataUrlAttribute("{0}", String.Join(root, "../", "{0}")).Value;
                res.Title = "Stored links";
            }
            return res;
        }

        static readonly int StoredUrlUrlIndex = TableDataTools.GetColumnIndex<StoredUrlAction>(nameof(StoredUrlAction.Url));

        readonly FastMemCache<string, Tuple<IReadOnlyList<StoredUrl>, String>> StoredUrlCache = new FastMemCache<string, Tuple<IReadOnlyList<StoredUrl>, String>>(TimeSpan.FromSeconds(14), StringComparer.Ordinal);

        #endregion // StoredUrl



        public PerfMonitor PerfMon { get; } = new PerfMonitor(nameof(UserStorageService));

        public IEnumerable<Stats> GetStats()
        {
            foreach (var x in StoredFileCache.GetStats(nameof(UserStorageService), "FileCache."))
                yield return x;
            foreach (var x in DeleteExceptions.GetStats(nameof(UserStorageService), "Delete."))
                yield return x;
        }

        UserStorageLink InternalLoadLink(String filename)
        {
            using var _ = PerfMon.Track(nameof(InternalLoadLink));
            ReadOnlyMemory<Byte> d = File.ReadAllBytes(filename);
            if (filename.FastEndsWith(CompExt))
                d = Comp.GetDecompressed(d.Span);
            return Ser.Create<UserStorageLink>(d);
        }

    }

    sealed class UserStorageLink
    {
        public string Url;
        public String[] Files;
    }

}
