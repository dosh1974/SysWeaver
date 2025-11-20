using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Data;
using SysWeaver.Net;
using SysWeaver.OsServices;

namespace SysWeaver.MicroService
{


    [RequiredDep<FolderSyncService>()]
    [WebApiUrl("../ServerManager")]
    public sealed partial class ServerManagerService : IDisposable, IHttpServerModule
    {

        public ServerManagerService(ServiceManager manager, ServerManagerParams p)
        {
            p = p ?? new ServerManagerParams();
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
                s.AddFolder(v);
            }
            UpdateTask = new PeriodicTask(UpdateMetrics, 5000);
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref UpdateTask, null)?.Dispose();
        }

        readonly int RemoveServiceBackupsDays;


        #region IHttpServerModule

        public String[] OnlyForPrefixes { get; } = ["ServerManager/Data/"];

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

        static HashSet<String> GetConfigs(String path, String name)
        {
            var h = new HashSet<String>(StringComparer.Ordinal);
            foreach (var x in Directory.GetFiles(path, name + ".*.json"))
            {
                var fn = Path.GetFileName(x);
                var l = fn.FastToLower();
                if (l.IndexOf(".lastgood.") >= 0)
                    continue;
                if (l.IndexOf(".deps.") >= 0)
                    continue;
                if (l.IndexOf(".replace.") >= 0)
                    continue;
                var bits = l.Split('.');
                var bl = bits.Length;
                if (bits.Length > 2)
                {
                    var date = bits[bl - 2];
                    var p = date.Replace('-', '_').Split('_');
                    if (p.Length == 6)
                    {
                        var ds = String.Concat(
                            p[0], '-',
                            p[1], '-',
                            p[2], ' ',
                            p[3], ':',
                            p[4], ':',
                            p[5]);
                        if (DateTime.TryParse(ds, out var res))
                            continue;
                    }
                }
                h.Add(fn);
            }
            foreach (var x in Directory.GetFiles(path, name + ".*.config"))
            {
                var fn = Path.GetFileName(x);
                var l = fn.FastToLower();
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
            SmFileInfo[] masterConfigs = GetConfigs(masterFolder, "*").OrderBy(x => x).Select(x => GetFileInfo(Path.Combine(masterFolder, x))).ToArray();
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

        const string AuditGroup = "ServerManager";

        [WebApi]
        [WebApiAuth]
        [WebApiAudit(AuditGroup)]
        public Task<SmServiceDetail> Restart(String serviceName, HttpServerRequest context)
            => DoVerb(serviceName, "restart", context);

        [WebApi]
        [WebApiAuth]
        [WebApiAudit(AuditGroup)]
        public Task<SmServiceDetail> Pause(String serviceName, HttpServerRequest context)
            => DoVerb(serviceName, "pause", context);

        [WebApi]
        [WebApiAuth]
        [WebApiAudit(AuditGroup)]
        public Task<SmServiceDetail> Continue(String serviceName, HttpServerRequest context)
            => DoVerb(serviceName, "continue", context);

        [WebApi]
        [WebApiAuth]
        [WebApiAudit(AuditGroup)]
        public Task<SmServiceDetail> Stop(String serviceName, HttpServerRequest context)
            => DoVerb(serviceName, "stop", context);

        [WebApi]
        [WebApiAuth]
        [WebApiAudit(AuditGroup)]
        public Task<SmServiceDetail> Start(String serviceName, HttpServerRequest context)
            => DoVerb(serviceName, "start", context);

        [WebApi]
        [WebApiAuth]
        [WebApiAudit(AuditGroup)]
        public Task<SmServiceDetail> Uninstall(String serviceName, HttpServerRequest context)
            => DoVerb(serviceName, "uninstall", context);

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


            return new SmVersionDetail(version);
        }


    }




}
