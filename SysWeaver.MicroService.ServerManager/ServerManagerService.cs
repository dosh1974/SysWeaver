using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Compression;
using SysWeaver.Data;
using SysWeaver.FolderSync;
using SysWeaver.Net;
using SysWeaver.OsServices;

namespace SysWeaver.MicroService
{


    [RequiredDep<FolderSyncService>()]
    [WebApiUrl("../ServerManager")]
    public sealed partial class ServerManagerService : IDisposable, IHttpServerModule, IFileRepoContainer
    {

        static readonly IReadOnlySet<String> ValidConfigExt = ReadOnlyData.Set<String>(StringComparer.Ordinal,
              ".txt",
              ".json",
              ".config"
        );

        sealed class BackupFileRepo : IFileRepo
        {
            readonly ServerManagerService Manager;

            public BackupFileRepo(String key, String discFolder, ServerManagerService manager, bool isKey = false)
            {
                Manager = manager;
                IsKey = isKey;
                Key = key;
                DiscFolder = discFolder;
                UploadAuth = isKey ? Roles.Admin : "";
                ValidExt = isKey ? ValidKeyExt : ValidConfigExt;
            }
            readonly bool IsKey;
            readonly String DiscFolder;

            public string Key { get; init; }

            public IReadOnlyList<FileHttpServerModuleFolder> ExposeFolders => null;



            static readonly IReadOnlySet<String> ValidKeyExt = ReadOnlyData.Set<String>(StringComparer.Ordinal,
                ".txt"
            );

            readonly IReadOnlySet<String> ValidExt;

            public string UploadAuth { get; init; }

            async ValueTask<FileUploadResult> CheckFile(FileUploadInfo file)
            {
                var dest = Path.Combine(DiscFolder, file.Name);
                if (File.Exists(dest))
                {
                    var h = await FileHash.GetHashAsync(dest).ConfigureAwait(false);
                    h = HashTools.ToHexHash(h);
                    if (h.FastEquals(file.Hash))
                        return FileUploadResult.AlreadyUploaded;
                }
                if (!ValidExt.Contains(file.GetExtension().FastToLower()))
                    return FileUploadResult.RefuseExtension;
                if (file.Length > (64 << 10))
                    return FileUploadResult.RefuseSize;
                return FileUploadResult.Upload;
            }

            public ValueTask<FileUploadResult[]> CanFileBeUploaded(FileUploadInfo[] info, HttpServerRequest r)
            {
                if (!IsKey)
                {
                    try
                    {
                        Key.SplitFirst('_', out var serviceName);
                        Manager.Validate(serviceName, r);
                    }
                    catch
                    {
                        return ValueTask.FromResult(ArrayExt.Create(info.Length, FileUploadResult.NotAuthorized));
                    }
                }
                return info.ConvertAsyncValue(CheckFile);
            }

            public async ValueTask<FileUploadResult> Upload(Stream s, FileUploadInfo file, HttpServerRequest r, ICompDecoder decoder)
            {
                if (!IsKey)
                {
                    try
                    {
                        Key.SplitFirst('_', out var serviceName);
                        Manager.Validate(serviceName, r);
                    }
                    catch
                    {
                        return FileUploadResult.NotAuthorized;
                    }
                }
                var res = await CheckFile(file).ConfigureAwait(false);
                var dest = Path.Combine(DiscFolder, file.Name);
                if (res.Result != FileUploadStatus.Upload)
                    return res;

                var a = Manager.Audit;
                HttpApiAudit ad = null;
                long id = 0;
                if (a != null)
                {
                    id = ApiAudit.GetId();
                    ad = new HttpApiAudit(String.Concat("Upload ", Key, '/', file.Name), AuditGroup);
                    a.OnApiBegin(id, r, ad, file.Hash);
                }
                try
                {
                    if (!ServiceHost.BackupConfig(dest, Manager.Manager))
                    {
                        if (a != null)
                            a.OnApiException(id, r, ad, new Exception("Backup failed"));
                        return FileUploadResult.Refuse;
                    }
                    var data = await s.ReadAllMemoryAsync().ConfigureAwait(false);
                    if (decoder != null)
                        data = decoder.GetDecompressed(data.Span);
                    var text = Encoding.UTF8.GetString(data.Span);
                    await FileExt.WriteMemoryAsync(dest, data, true).ConfigureAwait(false);
                    if (a != null)
                        a.OnApiEnd(id, r, ad, IsKey ? "** PROTECTED **" : text.LimitLength(2048));
                    Manager.Syncer.GetFolderData(Key);
                    r.Session.InvalidateCache();
                    r.Server.InvalidateCache();
                    return FileUploadResult.None;
                }
                catch (Exception ex)
                {
                    if (a != null)
                        a.OnApiException(id, r, ad, ex);
                    throw;
                }
            }
        }

        public IFileRepo[] Repos { get; init;  }

        readonly IApiAuditService Audit;

        public ServerManagerService(ServiceManager manager, ServerManagerParams p)
        {
            p = p ?? new ServerManagerParams();
            Audit = manager.TryGet<IApiAuditService>();
            var removeServiceBackupsDays = Math.Max(3, p.RemoveServiceBackupsDays);
            RemoveServiceBackupsDays = removeServiceBackupsDays;
            var s = manager.Get<FolderSyncService>();
            Manager = manager;
            Syncer = s;
            foreach (var f in p.Folders.Nullable())
            {
                f.Auth = f.Auth ?? p.SyncAuth;
                s.AddFolder(f);
            }

            var destFolders = PathTemplate.Resolve(String.IsNullOrEmpty(p.ServiceFolder) ? @"$(CommonApplicationData)\SysWeaver\ManagedServices" : p.ServiceFolder).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var f in destFolders)
            {
                PathExt.EnsureFolderExist(f);
                PathExt.AllowAllAccess(f);
            }
            var ss = Services;
            List<IFileRepo> repos = new List<IFileRepo>();
            repos.Add(new BackupFileRepo("Keys", @"C:\Keys", this, true));
            foreach (var f in p.Services.Nullable())
            {
                var df = f.DiscFolder;
                if (String.IsNullOrEmpty(df))
                {
                    df = Path.GetFullPath(Folders.SelectFolder(destFolders, f.Name));
                    df = Path.Combine(df, f.Name, "bin");
                    PathExt.EnsureFolderExist(df);
                    PathExt.AllowAllAccess(df);
                    PathExt.AllowAllAccess(Path.Combine(df, f.Name));
                }
                else
                {
                    df = PathTemplate.Resolve(df);
                }
                var v = new FolderSyncFolder
                {
                    Name = f.Name,
                    DiscFolder = df,
                    Compress = p.CompressServices,
                    Auth = f.SyncAuth ?? p.SyncAuth,
                    RemoveBackupsDays = removeServiceBackupsDays,
                    OnNewFolderAsync = OnNewFolder,
                    OnActivateAsync = OnServiceActivate,
                    OnDeactivateAsync = OnServiceDeactivate,
                };
                if (!ss.TryAdd(f.Name.FastToLower(), new SmServiceInfo(f, v, p)))
                    throw new Exception("Must have a unique name!");
                var folder = s.AddFolder(v);
                repos.Add(new BackupFileRepo("Current_" + f.Name, folder, this));
                if (f.MasterConfig)
                    repos.Add(new BackupFileRepo("Master_" + f.Name, Path.GetDirectoryName(folder), this));
                
            }
            Repos = repos.ToArray();
            UpdateTask = new PeriodicTask(UpdateMetrics, 5000);
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref UpdateTask, null)?.Dispose();
        }

        readonly int RemoveServiceBackupsDays;


        #region IHttpServerModule

        public String[] OnlyForPrefixes { get; } = 
        [
            "ServerManager/Data/"
        ];

        public IHttpRequestHandler Handler(HttpServerRequest context)
        {
            var lurl = context.LocalUrl.Split('/');
            if (lurl.Length != 4)
                return null;
            var serviceName = lurl[2];
            SmServiceInfo info;
            try
            {
                info = Validate(serviceName, context);
            }
            catch
            {
                return null;
            }
            var folder = info.Syncher.DiscFolder;
            folder = Path.GetDirectoryName(folder);
            var file = new FileInfo(Path.Combine(folder, lurl[3]));
            if (!file.Exists)
                return null;
            var ext = file.Extension.FastToLower();
            if (!OkExtensions.Contains(ext))
                return null;
            var mime = MimeTypeMap.GetMimeType(ext);
            return new FileHttpRequestHandler(mime, file, info.Options, true);
        }

        static readonly IReadOnlySet<String> OkExtensions = new HashSet<string>(StringComparer.Ordinal)
        {
            ".json", ".config",
        }.Freeze();

        #endregion//IHttpServerModule

        PeriodicTask UpdateTask;

        readonly ConcurrentDictionary<String, SmServiceInfo> Services = new ConcurrentDictionary<string, SmServiceInfo>(StringComparer.Ordinal);
        readonly ServiceManager Manager;

        static String FindServiceExe(String path)
        {
            foreach (var x in Directory.GetFiles(path, "*.exe"))
            {
                var fn = Path.GetFileName(x).FastToLower();
                if (!fn.FastEquals("createdump.exe"))
                    return x;
            }
            return null;
        }

        static bool IsValidConfigName(String filename)
        {
            var ext = Path.GetExtension(filename).FastToLower();
            if (!ValidConfigExt.Contains(ext))
                return false;
            return !filename.FastEquals("_FolderSync.txt");
        }

        static readonly IReadOnlyDictionary<int, char> BackLocs = new Dictionary<int, char>
        {
            { 4, '-' },
            { 7, '-' },
            { 10, '_' },
            { 13, '_' },
            { 16, '_' },
        }.Freeze();

        public bool IsBackupName(String filename, out string orgName)
        {
            orgName = null;
            var e = filename.LastIndexOf('.');
            if (e < 0)
                return false;
            var ext = filename.Substring(e);
            filename = filename.Substring(0, e);
            e = filename.LastIndexOf('.');
            if (e < 0)
                return false;
            orgName = filename.Substring(0, e) + ext;
            filename = filename.Substring(e + 1);
            if (filename.FastEquals("LastGood"))
                return true;
            if (filename.Length != 19)
                return false;
            var bl = BackLocs;
            for (int i = 0; i < 19; ++i)
            {
                var c = filename[i];
                if (bl.TryGetValue(i, out var m))
                {
                    if (m == c)
                        continue;
                }
                if (c < '0')
                    return false;
                if (c > '9')
                    return false;
            }
            return true;
        }

        static HashSet<String> GetConfigs(String path, String name)
        {
            var h = new HashSet<String>(StringComparer.Ordinal);
            foreach (var x in Directory.GetFiles(path))
            {
                var fn = Path.GetFileName(x);
                if (IsValidConfigName(fn))
                    h.Add(fn);
            }
            return h;
        }

        const String LogPrefix = "[ServerManager] ";

        async ValueTask<Exception> OnNewFolder(String name, String path, Func<String, ValueTask<int>> commandRunner)
        {
            if (!Services.TryGetValue(name.FastToLower(), out var si))
                return null;
            var ss = si.Service;
            if (!ss.MasterConfig)
                return null;
            var exe = FindServiceExe(path);
            if (exe == null)
                return null;
            var ename = Path.GetFileNameWithoutExtension(exe);
            var parent = Path.GetDirectoryName(path);
            var existing = GetConfigs(parent, ename);
            var m = Manager;
            Exception ex = null;
            foreach (var config in GetConfigs(path, ename).OrderBy(x => x).ToList())
            {
                var master = Path.Combine(parent, config);
                var version = Path.Combine(path, config);


                if (existing.Remove(config))
                {
                    m.AddMessage(String.Concat(LogPrefix, "Replacing config \"", version, "\" with master config"));
                    ex = ex ?? await PathExt.TryCopyFileAsync(master, version).ConfigureAwait(false);
                }
                else
                {
                    m.AddMessage(String.Concat(LogPrefix, "Creating new master config from \"", version, '"'));
                    ex = ex ?? await PathExt.TryCopyFileAsync(version, master).ConfigureAwait(false);
                }
            }
            foreach (var config in existing.OrderBy(x => x).ToList())
            {
                var master = Path.Combine(parent, config);
                var version = Path.Combine(path, config);
                m.AddMessage(String.Concat(LogPrefix, "Creating new config from master \"", version, '"'));
                ex = ex ?? await PathExt.TryCopyFileAsync(master, version).ConfigureAwait(false);
            }
            return ex;
        }

        readonly ExceptionTracker StatusEx = new ExceptionTracker();

        async ValueTask<ServiceStatus> CheckStatus(String exe)
        {
            ServiceStatus status = ServiceStatus.Unknown;
            try
            {
                await ExternalProcess.RunAsync(exe, "status", (text, err) =>
                {
                    foreach (var lx in text.Split('\n'))
                    {
                        try
                        {
                            var l = lx.Trim();
                            if (!l.FastStartsWith("Checking status:"))
                                continue;
                            var s = l.LastIndexOf(' ') + 1;
                            var e = l.LastIndexOf(']');
                            var num = l.Substring(s, e - s);
                            status = (ServiceStatus)int.Parse(num);
                        }
                        catch (Exception ex)
                        {
                            StatusEx.OnException(ex);
                        }
                    }
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                StatusEx.OnException(ex);
            }
            return status;


        }

        async ValueTask<int> RunCommand(String cmd, bool silent = false)
        {
            if (silent)
            {
                try
                {
                    var exe = SystemHelper.GetCommandAndArgs(out var args, cmd);
                    return await ExternalProcess.RunAsync(exe, args).ConfigureAwait(false);
                }
                catch
                {
                }
                return -42;
            }
            var m = Manager;
            using var _ = m.Tab();
            try
            {
                var exe = SystemHelper.GetCommandAndArgs(out var args, cmd);
                return await ExternalProcess.RunAsync(exe, args, (text, err) =>
                {
                    m.AddMessage(LogPrefix + text, err ? MessageLevels.Warning : MessageLevels.Info);
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                m.AddMessage(LogPrefix + "Failed to run!", ex, MessageLevels.Warning);
            }
            return -42;
        }

        async ValueTask<Exception> OnServiceActivate(String name, String path, Func<String, ValueTask<int>> commandRunner)
        {
            var exe = FindServiceExe(path);
            if (exe == null)
                return null;
            var res = await RunCommand(exe.ToQuoted() + " start").ConfigureAwait(false);
            if (res < 0)
                return new Exception("Failed to start service \"" + name + "\", error: " + res);
            return null;
        }


        async ValueTask<Exception> OnServiceDeactivate(String name, String path, Func<String, ValueTask<int>> commandRunner)
        {
            var exe = FindServiceExe(path);
            if (exe == null)
                return null;
            var res = await RunCommand(exe.ToQuoted() + " uninstall").ConfigureAwait(false);
            if (res < 0)
                return new Exception("Failed to uninstall service \"" + name + "\", error: " + res);
            return null;
        }

        readonly FolderSyncService Syncer;

        readonly int MaxUpdateConcurrency = Math.Max(2, (Environment.ProcessorCount + 1) >> 1);

        async ValueTask<bool> UpdateMetrics()
        {
            Dictionary<String, Process> procExes = new Dictionary<string, Process>(StringComparer.Ordinal);
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    var mod = p.MainModule.FileName;
                    procExes[mod] = p;
                }
                catch
                {
                }
            }
            var s = Services.Values.ToList();
            var l = new AsyncLock(MaxUpdateConcurrency);
            var maxAge = DateTime.UtcNow - TimeSpan.FromHours(24);
            await s.ProcessAsyncValue(async i =>
            {
                using var _ = await l.Lock().ConfigureAwait(false);
                var m = new SmServiceMetrics();
                var exe = FindServiceExe(i.Syncher.DiscFolder);
                if (exe != null)
                {
                    if (procExes.TryGetValue(exe, out var p))
                    {
                        try
                        {
                            m.ProcessHandle = (long)p.Id;
                            m.MemUsage = (long)p.WorkingSet64;
                            var l = i.LastCpu;
                            var now = Stopwatch.GetTimestamp();
                            var time = p.TotalProcessorTime;
                            m.TotalProcessorTime = time;
                            if (l != 0)
                            {
                                var du = (Decimal)(time - i.LastTotCpu).TotalSeconds;
                                var dt = (Decimal)(now - l) / (Decimal)Stopwatch.Frequency;
                                if (dt > 0)
                                    m.CpuUsage = Math.Max(0, Math.Min(100, (double)((du * 100) / (dt * Environment.ProcessorCount))));
                            }
                            i.LastCpu = now;
                            i.LastTotCpu = time;
                        }
                        catch
                        {
                        }
                    }
                    m.Status = await CheckStatus(exe).ConfigureAwait(false);
                }
                else
                {
                    m.Status = ServiceStatus.NotInstalled;
                }
                Interlocked.Exchange(ref i.Metrics, m);
                var h = i.History;
                h.Enqueue(m);
                while (h.TryPeek(out var o))
                {
                    if (o.Time >= maxAge)
                        break;
                    if (!h.TryDequeue(out o))
                        break;
                }
            }).ConfigureAwait(false);
            return true;
        }


        SmServiceInfo Validate(String serviceName, HttpServerRequest context)
        {
            serviceName = serviceName.FastToLower();
            if (!Services.TryGetValue(serviceName, out var info))
                throw new Exception("Unknown service!");
            if (!context.Session.IsValid(info.Auth))
                throw new Exception("Not authorized!");
            return info;
        }


        [WebApi]
        [WebApiAuth]
        [WebApiClientCache(4)]
        [WebApiRequestCache(3)]
        [WebApiRaw(HttpServerTools.JsonMime)]
        public ReadOnlyMemory<Byte> GetMem(String serviceName, HttpServerRequest context)
        {
            var info = Validate(serviceName, context);
            var min = DateTime.UtcNow - TimeSpan.FromMinutes(5);
            List<String> labels = new List<string>();
            List<double> values = new List<double>();
            double minVal = double.MaxValue;
            double maxVal = double.MinValue;
            foreach (var x in info.History)
            {
                var t = x.Time;
                if (t < min)
                    continue;
                var val = (Double)x.MemUsage / (1024.0 * 1024.0);
                labels.Add(t.ToString("HH:mm:ss"));
                values.Add(val);
                if (val < minVal)
                    minVal = val;
                if (val > maxVal)
                    maxVal = val;

            }
            const double hueMin = 120;
            const double hueMax = 0;
            const double dHue = hueMax - hueMin;
            var dval = maxVal - minVal;
            String[] colors;
            if (dval <= 0)
                colors = values.Convert(x => "#0f0");
            else
            {
                var sval = dHue / dval;
                colors = values.Convert(v =>
                {
                    var rgb = ColorTools.HsvToRgb((v - minVal) * sval + hueMin, 0.7, 0.9);
                    return HtmlColors.MakeHtmlColor(rgb);
                });
            }

            return ChartJsService.ChartSerialize(new ChartJsConfig
            {
                RefreshRate = 5000,
                Title = serviceName + " memory usage last hour",
                type = "bar",
                Precision = 1,
                ValidTypes = ["bar"],
                ValueSuffix = " MB",
                data = new ChartJsData
                {
                    labels = labels.ToArray(),
                    datasets = [
                        new ChartJsDataSet
                        {
                            label = "Memory usage",
                            categoryPercentage = 0.99,
                            barPercentage = 1,
                            data = values.ToArray(),
                            backgroundColor = colors,
                        }
                    ]
                },
                options = new ChartJsOptions
                {
                    barPercentage = 1,
                    plugins = new ChartJsPlugins
                    {
                        datalabels = new ChartJsDataLabels
                        {
                            display = false,
                        }
                    }
                }

            });
        }


        [WebApi]
        [WebApiAuth]
        [WebApiClientCache(4)]
        [WebApiRequestCache(3)]
        [WebApiRaw(HttpServerTools.JsonMime)]
        public ReadOnlyMemory<Byte> GetCpu(String serviceName, HttpServerRequest context)
        {
            var info = Validate(serviceName, context);
            var min = DateTime.UtcNow - TimeSpan.FromMinutes(5);
            List<String> labels = new List<string>();
            List<double> values = new List<double>();
            foreach (var x in info.History)
            {
                var t = x.Time;
                if (t < min)
                    continue;
                var val = x.CpuUsage;
                labels.Add(t.ToString("HH:mm:ss"));
                values.Add(val);
            }
            const double hueMin = 120;
            const double hueMax = 0;
            const double dHue = hueMax - hueMin;
            var sval = dHue / 100;
            var colors = values.Convert(v =>
            {
                var rgb = ColorTools.HsvToRgb(v * sval + hueMin, 0.7, 0.9);
                return HtmlColors.MakeHtmlColor(rgb);
            });

            return ChartJsService.ChartSerialize(new ChartJsConfig
            {
                RefreshRate = 5000,
                Title = serviceName + " Cpu usage last hour",
                type = "bar",
                Precision = 1,
                ValidTypes = ["bar"],
                ValueSuffix = "%",
                data = new ChartJsData
                {
                    labels = labels.ToArray(),
                    datasets = [
                        new ChartJsDataSet
                        {
                            label = "Cpu usage",
                            categoryPercentage = 0.99,
                            barPercentage = 1,
                            data = values.ToArray(),
                            backgroundColor = colors,
                            borderRadius = new ChartJsCorner
                            {
                                bottomLeft = 0,
                                bottomRight = 0,
                                topLeft = 0,
                                topRight = 0,
                            }
                        }
                    ],

                },
                options = new ChartJsOptions
                {
                    barPercentage = 1,
                    scales = new ChartJsScalesOptions
                    {
                        y = new ChartJsScaleOptions
                        {
                            min = 0,
                            //max = 100,
                        }
                    },
                    plugins = new ChartJsPlugins
                    {
                        datalabels = new ChartJsDataLabels
                        {
                            display = false,
                        },
                    }
                }

            });
        }


        static SmFileInfo GetFileInfo(String fullname)
        {
            var fi = new FileInfo(fullname);
            if (!fi.Exists)
                return null;
            return new SmFileInfo
            {
                Name = fi.Name,
                LastModified = fi.LastWriteTimeUtc,
                Size = fi.Length,
            };
        }

        [WebApi]
        [WebApiAuth]
        [WebApiClientCache(4)]
        [WebApiRequestCache(3)]
        public SmServiceDetail GetDetail(String serviceName, HttpServerRequest context)
            => InternalGetDetail(Validate(serviceName, context));

        SmServiceDetail InternalGetDetail(SmServiceInfo info)
        {
            var data = Syncer.GetFolderData(info.Service.Name).ToList();
            var discFolder = info.Syncher.DiscFolder;
            var exeName = FindServiceExe(discFolder);
            SmFileInfo logFile = null;
            SmFileInfo[] configs = null;
            if (exeName != null)
            {
                exeName = Path.GetFileName(exeName);
                var baseName = Path.GetFileNameWithoutExtension(exeName);
                configs = GetConfigs(discFolder, baseName).OrderBy(x => x).Select(x => GetFileInfo(Path.Combine(discFolder, x))).ToArray();
                logFile = GetFileInfo(Path.Combine(discFolder, baseName + ".log"));
            }
            var masterFolder = Path.GetDirectoryName(discFolder);
            SmFileInfo[] masterConfigs = null;
            if (info.Service.MasterConfig)
                masterConfigs = GetConfigs(masterFolder, "*").OrderBy(x => x).Select(x => GetFileInfo(Path.Combine(masterFolder, x))).ToArray();
            return new SmServiceDetail(info, data, exeName, logFile, configs, masterConfigs);
        }


        [WebApi]
        [WebApiAuth]
        [WebApiClientCache(4)]
        [WebApiRequestCache(3)]
        public SmServiceBrief[] GetServices(HttpServerRequest context)
        {
            var session = context.Session;
            var ss = Services;
            var l = Services.Count;
            List<SmServiceInfo> s = new List<SmServiceInfo>(l);
            foreach (var x in ss)
            {
                var i = x.Value;
                if (!session.IsValid(i.Auth))
                    continue;
                s.Add(i);
            }
            return s.Convert(info => new SmServiceBrief(info, Syncer.GetFolderData(info.Syncher.Name).ToList()));
        }

        const string AuditGroup = "ServerManager";


        async Task<SmServiceDetail> DoVerb(String serviceName, String verb, HttpServerRequest context)
        {
            var info = Validate(serviceName, context);
            var exe = FindServiceExe(info.Syncher.DiscFolder);
            if (exe == null)
                return null;
            var res = await RunCommand(exe.ToQuoted() + " " + verb).ConfigureAwait(false);
            if (res < 0)
                throw new Exception("Failed to start service \"" + serviceName + "\", error: " + res);
            context.Session.InvalidateCache();
            context.Server.InvalidateCache();
            info.Metrics.Status = await CheckStatus(exe).ConfigureAwait(false);
            return InternalGetDetail(info);
        }

        #region Verbs

        /// <summary>
        /// Restart a managed service
        /// </summary>
        /// <param name="serviceName">Name of the managed service</param>
        /// <param name="context"></param>
        /// <returns>Updated service details</returns>
        [WebApi]
        [WebApiAuth]
        [WebApiAudit(AuditGroup)]
        public Task<SmServiceDetail> Restart(String serviceName, HttpServerRequest context)
            => DoVerb(serviceName, "restart", context);


        /// <summary>
        /// Pause a managed service (no web requests will be allowed)
        /// </summary>
        /// <param name="serviceName">Name of the managed service</param>
        /// <param name="context"></param>
        /// <returns>Updated service details</returns>
        [WebApi]
        [WebApiAuth]
        [WebApiAudit(AuditGroup)]
        public Task<SmServiceDetail> Pause(String serviceName, HttpServerRequest context)
            => DoVerb(serviceName, "pause", context);

        /// <summary>
        /// Continue a managed service (after being paused)
        /// </summary>
        /// <param name="serviceName">Name of the managed service</param>
        /// <param name="context"></param>
        /// <returns>Updated service details</returns>
        [WebApi]
        [WebApiAuth]
        [WebApiAudit(AuditGroup)]
        public Task<SmServiceDetail> Continue(String serviceName, HttpServerRequest context)
            => DoVerb(serviceName, "continue", context);

        /// <summary>
        /// Stop a managed service (will restart when computer start)
        /// </summary>
        /// <param name="serviceName">Name of the managed service</param>
        /// <param name="context"></param>
        /// <returns>Updated service details</returns>
        [WebApi]
        [WebApiAuth]
        [WebApiAudit(AuditGroup)]
        public Task<SmServiceDetail> Stop(String serviceName, HttpServerRequest context)
            => DoVerb(serviceName, "stop", context);

        /// <summary>
        /// Start a managed service (install and start)
        /// </summary>
        /// <param name="serviceName">Name of the managed service</param>
        /// <param name="context"></param>
        /// <returns>Updated service details</returns>
        [WebApi]
        [WebApiAuth]
        [WebApiAudit(AuditGroup)]
        public Task<SmServiceDetail> Start(String serviceName, HttpServerRequest context)
            => DoVerb(serviceName, "start", context);

        /// <summary>
        /// Uninstall a managed service (stop and uninstall)
        /// </summary>
        /// <param name="serviceName">Name of the managed service</param>
        /// <param name="context"></param>
        /// <returns>Updated service details</returns>
        [WebApi]
        [WebApiAuth]
        [WebApiAudit(AuditGroup)]
        public Task<SmServiceDetail> Uninstall(String serviceName, HttpServerRequest context)
            => DoVerb(serviceName, "uninstall", context);


        #endregion//Verbs



        /// <summary>
        /// All synched folders as a table
        /// </summary>
        /// <param name="r"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth]
        [WebMenuTable(null, "Services", "Services", "Services", "../icons/settings.svg", -7)]
        [WebApiClientCache(4)]
        [WebApiRequestCache(3)]
        public TableData ServicesTable(TableDataRequest r, HttpServerRequest context)
            => TableDataTools.Get(r, 5000, GetServices(context));


        /// <summary>
        /// Returns stats for folders
        /// </summary>
        /// <returns></returns>
        [WebApi]
        [WebApiClientCache(15)]
        [WebApiRequestCache(14)]
        public async Task<SmChunkFolderStats[]> GetStorageStats()
        {
            var old = DateTime.UtcNow.AddDays(-(RemoveServiceBackupsDays + 30)).ToStartOfDay(12);
            var stats = await ContentDependentChunking.GetFolderStats(false, old).ConfigureAwait(false);
            var l = stats.Length;
            if (l > 1)
            {
                CdcFolderStats sum = null;
                for (int i = 1; i < l; ++i)
                    sum = (sum ?? stats[0]).Merge(stats[i]);
                var n = new CdcFolderStats[l + 1];
                n[0] = sum;
                for (int i = 0; i < l; ++i)
                    n[i + 1] = stats[i];
                stats = n;

            }
            return stats.Convert(x => new SmChunkFolderStats(x));
        }

        /// <summary>
        /// Statistics about the chunked storage
        /// </summary>
        /// <param name="r"></param>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth(Roles.Debug)]
        [WebMenuTable(null, "ChunkStorage", "Chunk storage", "Analysis of the chunk storage", "../icons/disc.svg", -6)]
        [WebApiClientCache(14)]
        [WebApiRequestCache(10)]
        public async Task<TableData> StorageStatsTable(TableDataRequest r)
            => TableDataTools.Get(r, 15000, await GetStorageStats().ConfigureAwait(false));


        /// <summary>
        /// Statistics about the managed folders
        /// </summary>
        /// <param name="r"></param>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        [WebMenuTable(null, "Folders", "All folders", "All the managed folders", "../icons/sync.svg", -5)]
        [WebApiClientCache(4)]
        [WebApiRequestCache(3)]
        public TableData FoldersTable(TableDataRequest r)
            => Syncer.SynchedFoldersTable(r);


        #region Version

        SmServiceInfo GetValidatedVersion(out FolderSyncService.Data d, String versionName, HttpServerRequest context)
        {
            var p = versionName.Split(',');
            var serviceName = p[0];
            var uploaded = DateTime.Parse(p[1]).ToUniversalTime();
            var versionFolderName = p[2];
            var info = Validate(serviceName, context);
            var data = Syncer.GetFolderData(info.Service.Name).ToList();
            d = null;
            foreach (var x in data)
            {
                if (x.Uploaded != uploaded)
                    continue;
                if (d != null)
                {
                    if (!x.DiscFolder.FastEquals(versionFolderName))
                        continue;
                }
                d = x;
            }
            if (d == null)
            {
                foreach (var x in data)
                {
                    var dt = (x.Uploaded - uploaded).TotalSeconds;
                    if (dt < 0)
                        dt = -dt;
                    if (dt > 1)
                        continue;
                    if (d != null)
                    {
                        if (!x.DiscFolder.FastEquals(versionFolderName))
                            continue;
                    }
                    d = x;
                }
            }
            return info;
        }

        /// <summary>
        /// Get version details
        /// </summary>
        /// <param name="versionName">ServiceName,UploadedTime,DiscFolder</param>
        /// <param name="context"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        [WebApi]
        [WebApiAuth]
        [WebApiClientCache(4)]
        [WebApiRequestCache(3)]
        public SmVersionDetail GetVersion(String versionName, HttpServerRequest context)
        {
            var info = GetValidatedVersion(out var version, versionName, context);
            if (version == null)
                throw new Exception("Version not found!");
            return new SmVersionDetail(info, version);
        }


        /// <summary>
        /// Get version details
        /// </summary>
        /// <param name="versionName">ServiceName,UploadedTime,DiscFolder</param>
        /// <param name="context"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        [WebApi]
        [WebApiAuth]
        [WebApiAudit(AuditGroup)]
        public async Task<bool> VersionActivate(String versionName, HttpServerRequest context)
        {
            var info = GetValidatedVersion(out var version, versionName, context);
            if (version == null)
                throw new Exception("Version not found!");

            if (!await Syncer.Activate(new FolderSyncOperation
            {
                Folder = info.Syncher.Name,
                DiscFolder = version.DiscFolder,
            }, context).ConfigureAwait(false))
                throw new Exception("Failed to activate \"" + version.DiscFolder + "\"");
            Syncer.GetFolderData(info.Syncher.Name);
            var exe = FindServiceExe(info.Syncher.DiscFolder);
            if (exe != null)
                info.Metrics.Status = await CheckStatus(exe).ConfigureAwait(false);
            context.Session.InvalidateCache();
            context.Server.InvalidateCache();
            return true;
        }

        /// <summary>
        /// Touch a version (set last access time to now)
        /// </summary>
        /// <param name="versionName">ServiceName,UploadedTime,DiscFolder</param>
        /// <param name="context"></param>
        /// <returns>Compressed file stats or null if not compressed</returns>
        [WebApi]
        [WebApiAuth]
        [WebApiAudit(AuditGroup)]
        public async Task<SmCompFileStats> VersionTouch(String versionName, HttpServerRequest context)
        {
            var info = GetValidatedVersion(out var version, versionName, context);
            if (version == null)
                throw new Exception("Version not found!");
            var ret = await Syncer.Touch(new FolderSyncOperation
            {
                Folder = info.Syncher.Name,
                DiscFolder = version.DiscFolder,
            }, context).ConfigureAwait(false);
            Syncer.GetFolderData(info.Syncher.Name);
            context.Session.InvalidateCache();
            context.Server.InvalidateCache();
            return ret == null ? null : new SmCompFileStats(ret);
        }

        /// <summary>
        /// Verify a compressed version (looking for missing chunks etc)
        /// </summary>
        /// <param name="versionName">ServiceName,UploadedTime,DiscFolder</param>
        /// <param name="context"></param>
        /// <returns>Compressed file stats or null if not compressed</returns>
        [WebApi]
        [WebApiAuth]
        public async Task<SmCompFileStats> VersionVerify(String versionName, HttpServerRequest context)
        {
            var info = GetValidatedVersion(out var version, versionName, context);
            if (version == null)
                throw new Exception("Version not found!");
            var ret = await Syncer.Verify(new FolderSyncOperation
            {
                Folder = info.Syncher.Name,
                DiscFolder = version.DiscFolder,
            }, true, context).ConfigureAwait(false);
            Syncer.GetFolderData(info.Syncher.Name);
            context.Session.InvalidateCache();
            context.Server.InvalidateCache();
            return ret == null ? null : new SmCompFileStats(ret);
        }

        /// <summary>
        /// Expand a compressed version to it's individual files
        /// </summary>
        /// <param name="versionName">ServiceName,UploadedTime,DiscFolder</param>
        /// <param name="context"></param>
        /// <returns>Compressed file stats or null if not compressed</returns>
        [WebApi]
        [WebApiAuth]
        [WebApiAudit(AuditGroup)]
        public async Task<SmCompFileStats> VersionExpand(String versionName, HttpServerRequest context)
        {
            var info = GetValidatedVersion(out var version, versionName, context);
            if (version == null)
                throw new Exception("Version not found!");
            var ret = await Syncer.Expand(new FolderSyncOperation
            {
                Folder = info.Syncher.Name,
                DiscFolder = version.DiscFolder,
            }, context).ConfigureAwait(false);
            Syncer.GetFolderData(info.Syncher.Name);
            context.Session.InvalidateCache();
            context.Server.InvalidateCache();
            return ret == null ? null : new SmCompFileStats(ret);
        }


        /// <summary>
        /// Expand a compressed version to it's individual files
        /// </summary>
        /// <param name="versionName">ServiceName,UploadedTime,DiscFolder</param>
        /// <param name="context"></param>
        /// <returns>True if successful</returns>
        [WebApi]
        [WebApiAuth]
        [WebApiAudit(AuditGroup)]
        public async Task<bool> VersionCompress(String versionName, HttpServerRequest context)
        {
            var info = GetValidatedVersion(out var version, versionName, context);
            if (version == null)
                throw new Exception("Version not found!");
            var ret = await Syncer.Compress(new FolderSyncOperation
            {
                Folder = info.Syncher.Name,
                DiscFolder = version.DiscFolder,
            }, context).ConfigureAwait(false);
            Syncer.GetFolderData(info.Syncher.Name);
            context.Session.InvalidateCache();
            context.Server.InvalidateCache();
            return ret;
        }

        /// <summary>
        /// Delete a version
        /// </summary>
        /// <param name="versionName">ServiceName,UploadedTime,DiscFolder</param>
        /// <param name="context"></param>
        /// <returns>True if successful</returns>
        [WebApi]
        [WebApiAuth]
        [WebApiAudit(AuditGroup)]
        public async Task<bool> VersionDelete(String versionName, HttpServerRequest context)
        {
            var info = GetValidatedVersion(out var version, versionName, context);
            if (version == null)
                throw new Exception("Version not found!");
            var ret = await Syncer.Remove(new FolderSyncOperation
            {
                Folder = info.Syncher.Name,
                DiscFolder = version.DiscFolder,
            }, context).ConfigureAwait(false);
            Syncer.GetFolderData(info.Syncher.Name);
            context.Session.InvalidateCache();
            context.Server.InvalidateCache();
            return ret;
        }

        #endregion//Version


        /// <summary>
        /// Use a master config file (copy it into the active version folder)
        /// </summary>
        /// <param name="data"></param>
        /// <param name="context"></param>
        /// <returns>True if sucessfully copied</returns>
        /// <exception cref="Exception"></exception>
        [WebApi]
        [WebApiAuth]
        [WebApiAudit(AuditGroup)]
        public async Task<bool> UseMasterConfig(SmConfigRequest data, HttpServerRequest context)
        {
            var info = Validate(data.ServiceName, context);
            if (!info.Service.MasterConfig)
                throw new Exception("Service is not configured to have master configs!");
            var sname = data.Config;
            if (!IsValidConfigName(sname))
                throw new Exception("Invalid config name!");
            var bin = info.Syncher.DiscFolder;
            var dname = Path.Combine(bin, sname);
            var p = Path.GetDirectoryName(bin);
            sname = Path.Combine(p, sname);
            if (!File.Exists(sname))
                throw new Exception("The master configuration file does not exist!");
            if (!ServiceHost.BackupConfig(dname, Manager))
                throw new Exception("Failed to backup exsiting file \"" + dname + "\"");
            var ex = await PathExt.TryCopyFileAsync(sname, dname).ConfigureAwait(false);
            if (ex != null)
                throw ex;
            Syncer.GetFolderData(info.Syncher.Name);
            context.Session.InvalidateCache();
            context.Server.InvalidateCache();
            return true;
        }

        /// <summary>
        /// Make a backup config file, the active config file
        /// </summary>
        /// <param name="data"></param>
        /// <param name="context"></param>
        /// <returns>True if sucessfully renamed</returns>
        /// <exception cref="Exception"></exception>
        [WebApi]
        [WebApiAuth]
        [WebApiAudit(AuditGroup)]
        public async Task<bool> ActivateConfig(SmConfigRequest data, HttpServerRequest context)
        {
            var info = Validate(data.ServiceName, context);
            var bin = info.Syncher.DiscFolder;
            if (data.IsMaster)
            {
                if (!info.Service.MasterConfig)
                    throw new Exception("Service is not configured to have master configs!");
                bin = Path.GetDirectoryName(bin);
            }
            var sname = data.Config;
            if (!IsValidConfigName(sname))
                throw new Exception("Invalid config name!");
            if (!IsBackupName(sname, out var dname))
                throw new Exception("Config is not a backup!");
           
            sname = Path.Combine(bin, sname);
            dname = Path.Combine(bin, dname);
            if (!File.Exists(sname))
                throw new Exception("The master configuration file does not exist!");
            if (!ServiceHost.BackupConfig(dname, Manager))
                throw new Exception("Failed to backup exsiting file \"" + dname + "\"");
            var ex = await PathExt.TryMoveFileAsync(sname, dname).ConfigureAwait(false);
            if (ex != null)
                throw ex;
            Syncer.GetFolderData(info.Syncher.Name);
            context.Session.InvalidateCache();
            context.Server.InvalidateCache();
            return true;
        }

        /// <summary>
        /// Make a backup config file, the active config file
        /// </summary>
        /// <param name="data"></param>
        /// <param name="context"></param>
        /// <returns>True if sucessfully renamed</returns>
        /// <exception cref="Exception"></exception>
        [WebApi]
        [WebApiAuth]
        [WebApiAudit(AuditGroup)]
        public async Task<bool> DeleteConfig(SmConfigRequest data, HttpServerRequest context)
        {
            var info = Validate(data.ServiceName, context);
            var bin = info.Syncher.DiscFolder;
            if (data.IsMaster)
            {
                if (!info.Service.MasterConfig)
                    throw new Exception("Service is not configured to have master configs!");
                bin = Path.GetDirectoryName(bin);
            }
            var sname = data.Config;
            if (!IsValidConfigName(sname))
                throw new Exception("Invalid config name!");
            sname = Path.Combine(bin, sname);
            if (!File.Exists(sname))
                throw new Exception("The configuration file does not exist!");


/*            if (!ServiceHost.BackupConfig(dname, Manager))
                throw new Exception("Failed to backup exsiting file \"" + dname + "\"");
            var ex = await PathExt.TryMoveFileAsync(sname, dname).ConfigureAwait(false);
            if (ex != null)
                throw ex;
            Syncer.GetFolderData(info.Syncher.Name);
            context.Session.InvalidateCache();
            context.Server.InvalidateCache();*/
            return true;
        }

    }

}
