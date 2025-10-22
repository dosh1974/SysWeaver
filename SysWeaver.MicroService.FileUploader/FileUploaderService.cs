using CommunityToolkit.HighPerformance;
using SysWeaver.Compression;
using SysWeaver.MicroService;
using SysWeaver.Net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.IO;
using System.Linq;

namespace SysWeaver.MicroService
{

    [WebApiUrl("../upload")]
    [IsMicroService]
    [OptionalDep<FileHttpServerModule, IFileRepo>]
    [WebMenuEmbedded(null, "Debug/FileUploader", "Upload file", "upload/Example.html", "Manually upload a file to some file repository", "IconDisc", -8, "debug,dev")]
    public sealed class FileUploaderService : IHttpServerModule, IDisposable, IHaveStats
    {
        public FileUploaderService(ServiceManager manager, FileUploaderParams p = null)
        {
            Manager = manager;
            p ??= new FileUploaderParams();
            FileMod = manager.TryGet<FileHttpServerModule>();
            var r = p.Repos;
            List<StaticFileRepo> prunes = new List<StaticFileRepo>(r?.Length ?? 0);
            if (r != null)
            {
                foreach (var rr in r)
                    AddRepo(rr);
            }
            foreach (var rr in manager.GetAll<IFileRepo>())
                AddRepo(rr);

            foreach (var rr in manager.GetAll<IFileRepoContainer>())
                AddRepos(rr);
          
            PruneFilesTask = new PeriodicTask(PruneFiles, 60 * 60 * 1000);
            manager.OnServiceAdded += OnServiceAdded;
            manager.OnServiceRemoved += OnServiceRemoved;
            const String uploadUrl = "upload/Upload";
            UploadUrl = uploadUrl;
            OnlyForPrefixes = [uploadUrl];
        }

        readonly ExceptionTracker PruneExceptions = new ExceptionTracker();
        long DelCount;

        async ValueTask<bool> PruneFiles()
        {
            var ext = PruneExceptions;
            foreach (var x in Repos)
            {
                var repo = x.Value.Item1 as StaticFileRepo;
                if (repo == null)
                    continue;
                var count = await repo.RemoveOldFilesNow(ext).ConfigureAwait(false);
                Interlocked.Add(ref DelCount, count);
            }
            return true;
        }

        PeriodicTask PruneFilesTask;


        public override string ToString()
        {
            var rs = Repos;
            var r = rs.Count;
            return String.Concat(r, r == 1 ? " file repositiory: " : " file repositories: ", String.Join(", ", rs.Select(x => x.Key.ToQuoted())));
        }

        FileHttpServerModule FileMod;
        readonly ServiceManager Manager;
        readonly String UploadUrl;

        public String[] OnlyForPrefixes { get; init;  }

        public void AddRepo(IFileRepo repo)
        {
            if (repo == null)
                return;
            var key = repo.Key;
            if (key == null)
                return;
            var repos = Repos;
            lock (repos)
            {
                if (repos.TryGetValue(key, out var x))
                {
                    foreach (var folder in x.Item1.ExposeFolders.Nullable())
                        FileMod?.RemoveFolder(folder);
                }
                repos[key] = Tuple.Create(repo, new UploadHandler(this, repo));
                foreach (var folder in repo.ExposeFolders.Nullable())
                    FileMod?.AddFolder(folder);
            }
        }

        public bool RemoveRepo(IFileRepo repo)
        {
            if (repo == null)
                return false;
            var key = repo.Key;
            if (key == null)
                return false;
            var repos = Repos;
            lock (repos)
            {
                if (!repos.TryGetValue(key, out var rr))
                    return false;
                if (rr.Item1 != repo)
                    return false;
                repos.TryRemove(key, out rr);
                return true;
            }
        }



        public void AddRepos(IFileRepoContainer container)
        {
            if (container == null)
                return;
            var repos = Repos;
            lock (repos)
            {
                foreach (var repo in container.Repos)
                    AddRepo(repo);
            }
        }


        public bool RemoveRepos(IFileRepoContainer container)
        {
            if (container == null)
                return false;
            var repos = Repos;
            bool r = true;
            lock (repos)
            {
                foreach (var repo in container.Repos)
                    r &= RemoveRepo(repo);
            }
            return r;
        }



        public void Dispose()
        {
            Interlocked.Exchange(ref PruneFilesTask, null)?.Dispose();
            var manager = Manager;
            manager.OnServiceRemoved -= OnServiceRemoved;
            manager.OnServiceAdded -= OnServiceAdded;
        }


        void OnServiceRemoved(object service, ServiceInfo info)
        {
            var ss = service as FileHttpServerModule;
            if (ss == FileMod)
            {
                FileMod = null;
                if (ss != null)
                {
                    foreach (var x in Repos.Values)
                    {
                        var repo = x.Item1;
                        foreach (var folder in repo.ExposeFolders.Nullable())
                            ss.RemoveFolder(folder);
                    }
                }
            }
            RemoveRepos(service as IFileRepoContainer);
            RemoveRepo(service as IFileRepo);
        }

        void OnServiceAdded(object service, ServiceInfo info)
        {
            var ss = service as FileHttpServerModule;
            if (ss != null)
            {
                if (FileMod == null)
                {
                    FileMod = ss;
                    foreach (var x in Repos.Values)
                    {
                        var repo = x.Item1;
                        foreach (var folder in repo.ExposeFolders.Nullable())
                            ss.AddFolder(folder);
                    }
                }
            }
            AddRepo(service as IFileRepo);
            AddRepos(service as IFileRepoContainer);
        }

        readonly ConcurrentDictionary<String, Tuple<IFileRepo, UploadHandler>> Repos = new ConcurrentDictionary<string, Tuple<IFileRepo, UploadHandler>>();


       

        /// <summary>
        /// Check upload status for one or more files
        /// </summary>
        /// <param name="req"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        [WebApi]
        public async Task<FileUploadResult[]> CheckStatus(FileUploadRequest req, HttpServerRequest context)
        {
            if (!Repos.TryGetValue(req.Repo, out var repo))
                return ArrayExt.Create(req.Files.Length, FileRepoTools.UnknownRepo);
            var a = repo.Item2.Auth;
            if (a != null)
            {
                var au = context.Session?.Auth;
                if (!(au?.IsValid(a) ?? false))
                    return ArrayExt.Create(req.Files.Length, FileRepoTools.NotAuthorized);
            }
            return await repo.Item1.CanFileBeUploaded(req.Files, context).ConfigureAwait(false);
        }


        /// <summary>
        /// Determine if the request can be handled by this module
        /// </summary>
        /// <param name="context">The incoming request</param>
        /// <returns>A handler for the request or null if it can't be handled by this module</returns>
        public IHttpRequestHandler Handler(HttpServerRequest context)
        {
            if (context.HttpMethod != HttpServerMethods.POST)
                return null;
            var p = context.QueryParamsLowercase;
            if (!p.TryGetValue("repo", out var repoName))
                return InvalidParams;
            if (!Repos.TryGetValue(repoName, out var repo))
                return InvalidParams;
            return repo.Item2;
        }


        static readonly GenericHttpRequestHandler InvalidParams = HttpServerTools.GetPlainTextHandler(((int)FileUploadStatus.InvalidParams).ToString());


        const String LocationPrefix = "[FileUploader]";

        /// <summary>
        /// Enumerate all enpoints
        /// </summary>
        /// <param name="root">If null all endpoints are returned (recursively)</param>
        /// <returns>End point information</returns>
        public IEnumerable<IHttpServerEndPoint> EnumEndPoints(String root = null)
        {
            if (root == null)
            {
                yield return new HttpServerEndPoint(UploadUrl, LocationPrefix + " used to upload files", EnvInfo.AppStart, HttpServerEndpointTypes.FileUpload, "POST");
            }
            else
            {
                var s = UploadUrl.Split('/');
                var sl = s.Length;
                var r = root.Split('/', StringSplitOptions.RemoveEmptyEntries);
                var rl = r.Length;
                if (rl < sl)
                {
                    int i;
                    bool ok = true;
                    for (i = 0; ok && (i < rl); ++i)
                        ok = s[i] == r[i];
                    if (ok)
                    {
                        if ((i + 1) >= sl)
                        {
                            yield return new HttpServerEndPoint(UploadUrl, LocationPrefix + " used to upload files", EnvInfo.AppStart, HttpServerEndpointTypes.FileUpload, "POST");
                        }
                        else
                        {
                            yield return new HttpServerEndPoint(String.Join('/', s, 0, i + 1), "[Implicit Folder] from " + LocationPrefix, EnvInfo.AppStart);
                        }
                    }
                }
            }
        }


        internal async ValueTask<FileUploadResult> Upload(HttpServerRequest context, IFileRepo repo)
        {
            try
            {
                var p = context.QueryParamsLowercase;
                if (!p.TryGetValue("name", out var name))
                    return FileRepoTools.InvalidParams;
                if (!p.TryGetValue("hash", out var hash))
                    return FileRepoTools.InvalidParams;
                if (!p.TryGetValue("length", out var lstr))
                    return FileRepoTools.InvalidParams;
                if (!long.TryParse(lstr, out var length))
                    return FileRepoTools.InvalidParams;
                if (!p.TryGetValue("time", out var tstr))
                    return FileRepoTools.InvalidParams;
                if (!Double.TryParse(tstr, CultureInfo.InvariantCulture, out var time))
                    return FileRepoTools.InvalidParams;
                var fi = new FileUploadInfo
                {
                    Name = name,
                    Length = length,
                    Hash = hash,
                    LastModified = time,
                };
                var res = (await repo.CanFileBeUploaded([fi], context).ConfigureAwait(false))[0];
                if (res.Result != FileUploadStatus.Upload)
                    return res;
                var compName = context.GetReqHeader("Content-Encoding");
                ICompDecoder comp = compName != null ? CompManager.GetFromHttp(compName) : null;
                var ures = await repo.Upload(context.InputStream, fi, context, comp).ConfigureAwait(false);
                Interlocked.Increment(ref FileCount);
                Interlocked.Add(ref FileBytes, length);
                return ures;
            }
            catch (Exception ex)
            {
                UploadFailures.OnException(ex);
                return FileRepoTools.UploadFailed;
            }
        }

        long FileCount;
        long FileBytes;

        readonly ExceptionTracker UploadFailures = new ExceptionTracker();

        public IEnumerable<Stats> GetStats()
        {
            yield return new Stats(nameof(FileUploaderService), nameof(FileCount), Interlocked.Read(ref FileCount), "Number of files that have been uploaded since the service started");
            yield return new Stats(nameof(FileUploaderService), nameof(FileBytes), Interlocked.Read(ref FileBytes), "Number of bytes that have been uploaded since the service started", Data.TableDataByteSizeAttribute.Instance);
            foreach (var x in UploadFailures.GetStats(nameof(FileUploaderService), "Upload."))
                yield return x;
            if (PruneFilesTask != null)
            {
                yield return new Stats(nameof(FileUploaderService), nameof(DelCount), Interlocked.Read(ref DelCount), "Number of files deleted due to pruning");
                foreach (var x in PruneExceptions.GetStats(nameof(FileUploaderService), "Prune."))
                    yield return x;
            }
        }

    }





}
