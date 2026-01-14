using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Data;
using SysWeaver.FolderSync;
using SysWeaver.Net;
using SysWeaver.OsServices;

namespace SysWeaver.MicroService
{

    [RequiredDep<FolderSyncService>()]
    [WebApiUrl("../ServerManager")]
    [WebMenuEmbedded(null, "Server", "Server", "ServerManager/server.html", "View server stats", "../icons/computer.svg", -9, "")]
    [WebMenuEmbedded(null, "Services", "Managed services", "ServerManager/services.html", "View all managed services", "../icons/settings.svg", -7, "")]
    [WebMenuEmbedded(null, "Keys", "Key files", "ServerManager/keys.html", "Managed key files located in the key file folder", "../icons/key.svg", -6, Roles.Admin)]
    public sealed partial class ServerManagerService : IDisposable, IHttpServerModule
    {

        readonly String KeyFolder = @"C:\Keys";

        static readonly IReadOnlySet<String> ValidConfigExt = ReadOnlyData.Set<String>(StringComparer.Ordinal,
              ".txt",
              ".json",
              ".config" 
        );


        readonly IApiAuditService Audit;

        const String ServerManagerServicesKey = "ServerManagerServices";

        readonly String[] DestFolders;
        readonly ServerManagerParams P;
        readonly FileUploaderService FileUploader;
        readonly IFileRepo KeyRepo;

        public ServerManagerService(ServiceManager manager, ServerManagerParams p)
        {
            p = p ?? new ServerManagerParams();
            P = p;
            Audit = manager.TryGet<IApiAuditService>();
            var removeServiceBackupsDays = Math.Max(3, p.RemoveServiceBackupsDays);
            RemoveServiceBackupsDays = removeServiceBackupsDays;
            var s = manager.Get<FolderSyncService>();
            FileUploader = manager.Get<FileUploaderService>();
            Manager = manager;
            Syncer = s;
            foreach (var f in p.Folders.Nullable())
            {
                f.Auth = f.Auth ?? p.SyncAuth;
                s.AddFolder(f);
            }

            var destFolders = PathTemplate.Resolve(String.IsNullOrEmpty(p.ServiceFolder) ? @"$(CommonApplicationData)\SysWeaver\ManagedServices" : p.ServiceFolder).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var f in destFolders)
                PathExt.CreateDataFolder(f);
            DestFolders = destFolders;
            var ss = Services;
            KeyRepo = new BackupFileRepo("Keys", KeyFolder, this, true);
            FileUploader.AddRepo(KeyRepo);

            var savedServices = KeyValueStore.AllApp.TryGet<ManagedService[]>(ServerManagerServicesKey);
            foreach (var f in p.Services.Nullable().Concat(savedServices.Nullable()))
                InternalAddService(f);
            UpdateStats().RunAsync();
            UpdateTask = new PeriodicTask(UpdateMetrics, 4000);
            UpdateStatsTask = new PeriodicTask(UpdateStats, 200);
        }


        void InternalAddService(ManagedService f)
        {
            var p = P;
            var df = f.DiscFolder;
            if (String.IsNullOrEmpty(df))
            {
                df = Path.GetFullPath(Folders.SelectFolder(DestFolders, f.Name));
                df = Path.Combine(df, f.Name, "bin");
            }
            else
            {
                df = PathTemplate.Resolve(df);
            }
            PathExt.CreateDataFolder(df);
            PathExt.SetupDataFolder(Path.GetDirectoryName(df));
            var v = new FolderSyncFolder
            {
                Name = f.Name,
                DiscFolder = df,
                Compress = p.CompressServices,
                Auth = f.SyncAuth ?? p.SyncAuth,
                RemoveBackupsDays = RemoveServiceBackupsDays,
                OnNewFolderAsync = OnNewFolder,
                OnActivateAsync = OnServiceActivate,
                OnDeactivateAsync = OnServiceDeactivate,
            };
            var folder = Syncer.AddFolder(v);
            var currentRepo = new BackupFileRepo("Current_" + f.Name, folder, this);
            var masterRepo = f.MasterConfig ? new BackupFileRepo("Master_" + f.Name, Path.GetDirectoryName(folder), this) : null;
            if (!Services.TryAdd(f.Name.FastToLower(), new SmServiceInfo(f, v, p, currentRepo, masterRepo)))
            {
                Syncer.RemoveFolder(v);
                throw new Exception("Must have a unique name!");
            }
            FileUploader.AddRepo(currentRepo);
            FileUploader.AddRepo(masterRepo);
        }

        bool InternalRemoveService(String serviceName)
        {
            if (!Services.TryRemove(serviceName.FastToLower(), out var info))
                return false;
            Syncer.RemoveFolder(info.Syncher);
            FileUploader.RemoveRepo(info.Master);
            FileUploader.RemoveRepo(info.Current);
            return true;
        }


        public void Dispose()
        {
            Interlocked.Exchange(ref UpdateStatsTask, null)?.Dispose();
            Interlocked.Exchange(ref UpdateTask, null)?.Dispose();
            var fu = FileUploader;
            var sy = Syncer;
            var ss = Services;
            var d = ss.Keys.ToList();
            foreach (var k in d)
            {
                if (!ss.TryGetValue(k, out var s))
                    continue;
                fu.RemoveRepo(s.Master);
                fu.RemoveRepo(s.Current);
                sy.RemoveFolder(s.Syncher);
            }
            fu.RemoveRepo(KeyRepo);
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
        PeriodicTask UpdateStatsTask;

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
            var masterConfigs = GetConfigs(parent, ename);
            var m = Manager;
            Exception ex = null;
            foreach (var config in GetConfigs(path, ename).OrderBy(x => x).ToList())
            {
                if (ServiceHost.IsConfigBackupName(config, out var o))
                    continue;
                var master = Path.Combine(parent, config);
                var version = Path.Combine(path, config);
                if (masterConfigs.Remove(config))
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
            foreach (var config in masterConfigs.OrderBy(x => x).ToList())
            {
                if (ServiceHost.IsConfigBackupName(config, out var o))
                    continue;
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
            m.AddMessage(LogPrefix + "Executing: " + cmd);
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
        
        
        async ValueTask<bool> UpdateStats()
        {
            var os = PlatformTools.Current;
            if (os.GetMemorySize(out var free, out var tot))
            {
                double used = (double)((tot - free) * 100M / Math.Max(1M, tot));
                MemInfo = new SmMemoryInfo
                {
                    Free = (long)free,
                    Total = (long)tot,
                    Used = used
                };
                MemUsageHistory.Add(used);
                MemUsageHistoryShort.Add(used);
            }
            if (os.GetCpuUsage(out var cpu))
            {
                CpuUsage = (float)cpu;
                CpuUsageHistory.Add(cpu);
                CpuUsageHistoryShort.Add(cpu);
            }
            return true;
        }




        static T SafeRead<T>(Func<T> f, T def = default)
        {
            try
            {
                return f();
            }
            catch
            {
            }
            return def;
        }

        readonly ConcurrentDictionary<long, SmProcess> Processes = new ();

        async ValueTask<bool> UpdateMetrics()
        {
            Dictionary<String, SmProcess> procExes = new Dictionary<string, SmProcess>(StringComparer.Ordinal);
            var pp = Processes;
            HashSet<long> current = new (pp.Keys);
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    var id = (long)p.Id;
                    current.Remove(id);
                    if (!pp.TryGetValue(id, out var i))
                    {
                        i = new SmProcess(id,
                            SafeRead(() => p.ProcessName),
                            SafeRead(() => p.StartTime),
                            SafeRead(() => p.MainModule?.FileName)
                            );
                        pp.TryAdd(id, i);
                    }

                    var lastCpu = i.LastCpu;
                    var now = Stopwatch.GetTimestamp();
                    var time = SafeRead(() => p.TotalProcessorTime);
                    var m = new SmProcessMetrics(i)
                    {
                        HandleCount = SafeRead(() => p.HandleCount),

                        MemUsage = SafeRead(() => p.WorkingSet64),
                        PeakMemUsage = SafeRead(() => (long)p.PeakWorkingSet64),
                        MaxMemUsage = SafeRead(() => (long)p.MaxWorkingSet),

                        PriorityClass = SafeRead(() =>
                        {
                            var c = p.PriorityClass;
                            return String.Concat("0x", ((int)c).ToString("x").PadLeft(4, '0'), ": ", c.ToString());
                        }),
                        BasePriority = SafeRead(() => p.BasePriority),
                        PriorityBoost = SafeRead(() => p.PriorityBoostEnabled),

                        NonpagedSystemMemory = SafeRead(() => p.NonpagedSystemMemorySize64),
                        PagedSystemMemory = SafeRead(() => p.PagedSystemMemorySize64),

                        PagedMemory = SafeRead(() => p.PagedMemorySize64),
                        PeakPagedMemory = SafeRead(() => p.PeakPagedMemorySize64),

                        VirtualMemory = SafeRead(() => p.VirtualMemorySize64),
                        PeakVirtualMemory = SafeRead(() => p.PeakVirtualMemorySize64),

                        PrivateMemory = SafeRead(() => p.PrivateMemorySize64),

                        TotalCpuTime = time,
                        TotalSystemCpuTime = SafeRead(() => p.PrivilegedProcessorTime),
                        TotalUserCpuTime = SafeRead(() => p.UserProcessorTime),
                    };
                    if (lastCpu != 0)
                    {
                        var du = (Decimal)(time - i.LastTotCpu).TotalSeconds;
                        var dt = (Decimal)(now - lastCpu) / (Decimal)Stopwatch.Frequency;
                        if (dt > 0)
                            m.CpuUsage = Math.Max(0, Math.Min(100, (double)((du * 100) / (dt * Environment.ProcessorCount))));
                    }
                    i.LastCpu = now;
                    i.LastTotCpu = time;
                    var fn = i.MainFilename;
                    if (fn != null)
                        procExes[fn] = i;

                    Interlocked.Exchange(ref i.Metrics, m);
                    var nd = new SmServiceData { MemUsage = m.MemUsage, CpuUsage = m.CpuUsage };
                    i.History.Add(nd);
                    i.HistoryShort.Add(nd);
                }
                catch
                {
                }
            }
            foreach (var x in current)
                pp.TryRemove(x, out var _);

            var s = Services.Values.ToList();
            var l = new AsyncLock(MaxUpdateConcurrency);
            var maxAge = DateTime.UtcNow - TimeSpan.FromHours(24);
            await s.ProcessAsyncValue(async i =>
            {
                using var _ = await l.Lock().ConfigureAwait(false);
                var exe = FindServiceExe(i.Syncher.DiscFolder);
                if (exe != null)
                {
                    if (procExes.TryGetValue(exe, out var p))
                        Interlocked.Exchange(ref i.Process, p);
                    Interlocked.Exchange(ref i.Status, await CheckStatus(exe).ConfigureAwait(false));
                }
                else
                {
                    Interlocked.Exchange(ref i.Status, ServiceStatus.NotInstalled);
                }
            }).ConfigureAwait(false);

            try
            {
                var now = DateTime.UtcNow;
                var drives = DriveInfo.GetDrives();
                drives.Sort((a, b) => a.Name.CompareTo(b.Name));
                var du = DriveUsage;

                int driveIndex = -1;
                var dis = drives.Select(x =>
                {
                    ++driveIndex;
                    var d = x.Name;
                    if (!du.TryGetValue(driveIndex, out var h))
                    {
                        lock (du)
                        {
                            if (!du.TryGetValue(driveIndex, out h))
                            {
                                h = (d, 
                                    new BucketValueHistory<double>(TimeSpan.FromHours(1), TimeSpan.FromDays(3), (a, b) => a + b),
                                    new BucketValueHistory<double>(TimeSpan.FromDays(1), TimeSpan.FromDays(90), (a, b) => a + b)
                                    );
                                du.TryAdd(driveIndex, h);
                            }
                        }
                    }
                    var free = x.TotalFreeSpace;
                    var tot = x.TotalSize;
                    var used = (double)((tot - free) * 100M / Math.Max(1M, tot));
                    h.Item2.Add(used, now);
                    h.Item3.Add(used, now);
                    return new SmDriveInfo
                    {
                        Index = driveIndex,
                        Drive = d,
                        Label = x.VolumeLabel,
                        Format = x.DriveFormat,
                        Type = x.DriveType.ToString(),
                        Free = free,
                        Total = tot,
                        Used = used
                    };
                }).Where(x => x != null).ToArray();
                DriveInfos = dis;
            }
            catch (Exception ex)
            {
                DriveEx.OnException(ex);
            }
            return true;
        }

        readonly ExceptionTracker DriveEx = new ExceptionTracker();

        readonly ConcurrentDictionary<int, ValueTuple<String, BucketValueHistory<double>, BucketValueHistory<double>>> DriveUsage = new ();


        SmServiceInfo Validate(String serviceName, HttpServerRequest context)
        {
            serviceName = serviceName.FastToLower();
            if (!Services.TryGetValue(serviceName, out var info))
                throw new Exception("Unknown service!");
            if (!context.Session.IsValid(info.Auth))
                throw new Exception("Not authorized!");
            return info;
        }

        const decimal GbSize = 1024M * 1024M * 1024M;

        static ReadOnlyMemory<Byte> GetHistoryChart<T>(BucketValueHistory<T> h, Func<T, double> getValue, String title, String label, String valueSuffix, TimeSpan duration, String[] colors, String timeFmt = "HH:mm:ss", double scale = 1, int precision = 1)
        {
            var min = DateTime.UtcNow - duration;
            List<String> labels = new (128);
            List<double> values = new (128);
            if (h != null)
            {
                foreach (var x in h)
                {
                    var t = x.Item3;
                    if (t < min)
                        continue;
                    var val = scale * getValue(x.Item5) / x.Item4;
                    labels.Add(x.Item1.ToString(timeFmt));
                    values.Add(val);
                }
            }
            return ChartJsService.ChartSerialize(new ChartJsConfig
            {
                RefreshRate = 2000,
                Title = title,
                type = "bar",
                Precision = precision,
                ValidTypes = ["bar"],
                ValueSuffix = valueSuffix,
                ValueLabel = 4,
                data = new ChartJsData
                {
                    labels = labels.ToArray(),
                    datasets = [
                        new ChartJsDataSet
                        {
                            label = label,
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
                        title = new ChartJsTitle
                        {
                            display = true,
                            text = [ title ],
                        }
                    }
                }
            });

        }

        static String[] DoughnutChartColor(String start, String end)
        {
            var s = HtmlColors.MakeTransparent(start, 0.2);
            var e = HtmlColors.MakeTransparent(end, 0.2);
            return [
                String.Concat("cone(0;", start, ";1;", end, ')'),
                String.Concat("cone(0;", s, ";1;", e, ')'),
            ];
        }


        static String[] BarChartColor(String start, String end)
        {
            return [
                String.Concat("up(0;", start, ";1;", end, ')'),
            ];
        }


        static readonly String[] DoughnutServiceColor = DoughnutChartColor("#966C90", "#DE8CFF");


        static readonly String[] DoughnutCpuColor = DoughnutChartColor("#CC4343", "#EDDD53");
        
        static readonly String[] BarCpuColor = BarChartColor("#CC4343", "#EDDD53");


        static readonly String[] DoughnutMemColor = DoughnutChartColor("#43CC8C", "#53DEED");

        static readonly String[] BarMemColor = BarChartColor("#43CC8C", "#53DEED");


        static readonly String[] DoughnutDriveColor = DoughnutChartColor("#435ACC", "#ED53BF");

        static readonly String[] BarDriveColor = BarChartColor("#435ACC", "#ED53BF");

        #region Process info

        /// <summary>
        /// Information about running processes
        /// </summary>
        /// <param name="r"></param>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        [WebMenuTable(null, "ProcessInfo", "Process information", "Information about running processes", "../icons/table_services.svg", -5)]
        [WebApiClientCache(4)]
        [WebApiRequestCache(3)]
        public TableData ProcessInfoTable(TableDataRequest r)
            => TableDataTools.Get(r, 5000, Processes.Values.Select(x => x.Metrics).Where(x => x != null));

        SmProcess ValidateProcess(long processId)
        {
            if (!Processes.TryGetValue(processId, out var i))
                throw new Exception(String.Concat("Process #", processId, " not found!"));
            return i;
        }

        SmProcessMetrics ValidateProcessMetrics(long processId)
        {
            if (!Processes.TryGetValue(processId, out var i))
                throw new Exception(String.Concat("Process #", processId, " not found!"));
            var m = i.Metrics;
            if (m == null)
                throw new Exception(String.Concat("Can't get information about process #", processId));
            return m;
        }

        /// <summary>
        /// Get memory graph for a process
        /// </summary>
        /// <param name="processId">Id of the process</param>
        /// <returns>Graph data as json</returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        [WebApiClientCache(4)]
        [WebApiRequestCache(3)]
        [WebApiRaw(HttpServerTools.JsonMime)]
        public ReadOnlyMemory<Byte> GetProcessMemChart(long processId)
        {
            var info = ValidateProcessMetrics(processId);
            var mi = MemInfo;
            if (mi == null)
                return null;
            var tot = mi.Total;
            var used = info.MemUsage;
            double usedP = (double)(used * 100M / Math.Max(1M, tot));
            return GetMemChart(Math.Max(0, tot - used), tot, usedP, String.Concat(processId, ": ", info.Name, " memory use"));
        }


        /// <summary>
        /// Get Cpu graph for a process
        /// </summary>
        /// <param name="processId">Id of the process</param>
        /// <returns>Graph data as json</returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        [WebApiClientCache(4)]
        [WebApiRequestCache(3)]
        [WebApiRaw(HttpServerTools.JsonMime)]
        public ReadOnlyMemory<Byte> GetProcessCpuChart(long processId)
        {
            var info = ValidateProcessMetrics(processId);
            return GetCpuChart(info.CpuUsage, String.Concat(processId, ": ", info.Name, " Cpu use"));
        }

        /// <summary>
        /// Get Memory graph for a process
        /// </summary>
        /// <param name="processId">Id of the process</param>
        /// <returns>Graph data as json</returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        [WebApiClientCache(4)]
        [WebApiRequestCache(3)]
        [WebApiRaw(HttpServerTools.JsonMime)]
        public ReadOnlyMemory<Byte> GetProcessMemHistoryChart(long processId)
        {
            var info = ValidateProcess(processId);
            return GetHistoryChart(info.History, x => x.MemUsage, String.Concat(processId, ": ", info.Name, " memory use last 24 hours"), "Memory use", "Mb", TimeSpan.FromHours(24), BarMemColor, "HH:mm", 1.0 / (1024 * 1024), 2);
        }


        /// <summary>
        /// Get Cpu graph for a process
        /// </summary>
        /// <param name="processId">Id of the process</param>
        /// <returns>Graph data as json</returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        [WebApiClientCache(4)]
        [WebApiRequestCache(3)]
        [WebApiRaw(HttpServerTools.JsonMime)]
        public ReadOnlyMemory<Byte> GetProcessCpuHistoryChart(long processId)
        {
            var info = ValidateProcess(processId);
            return GetHistoryChart(info.History, x => x.CpuUsage, String.Concat(processId, ": ", info.Name, " Cpu use last 24 hours"), "Cpu use", "%", TimeSpan.FromHours(24), BarCpuColor, "HH:mm");
        }

        /// <summary>
        /// Get Memory graph for a process
        /// </summary>
        /// <param name="processId">Id of the process</param>
        /// <returns>Graph data as json</returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        [WebApiClientCache(4)]
        [WebApiRequestCache(3)]
        [WebApiRaw(HttpServerTools.JsonMime)]
        public ReadOnlyMemory<Byte> GetProcessMemHistoryShortChart(long processId)
        {
            var info = ValidateProcess(processId);
            return GetHistoryChart(info.HistoryShort, x => x.MemUsage, String.Concat(processId, ": ", info.Name, " memory use last 3 minutes"), "Memory use", "Mb", TimeSpan.FromMinutes(3), BarMemColor, "HH:mm:ss", 1.0 / (1024 * 1024), 2);
        }


        /// <summary>
        /// Get Cpu graph for a process
        /// </summary>
        /// <param name="processId">Id of the process</param>
        /// <returns>Graph data as json</returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        [WebApiClientCache(4)]
        [WebApiRequestCache(3)]
        [WebApiRaw(HttpServerTools.JsonMime)]
        public ReadOnlyMemory<Byte> GetProcessCpuHistoryShortChart(long processId)
        {
            var info = ValidateProcess(processId);
            return GetHistoryChart(info.HistoryShort, x => x.CpuUsage, String.Concat(processId, ": ", info.Name, " Cpu use last 3 minutes"), "Cpu use", "%", TimeSpan.FromMinutes(3), BarCpuColor, "HH:mm:ss");
        }



        #endregion//Process info



        #region CPU info

        float CpuUsage;

        /// <summary>
        /// Get the current CPU use as a percentage
        /// </summary>
        /// <returns>[0, 100] current cpu use</returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        [WebApiClientCache(1)]
        [WebApiRequestCache(1)]
        public float GetCpuUsage() => CpuUsage;


        /// <summary>
        /// Current CPU use chart
        /// </summary>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        [WebApiClientCache(1)]
        [WebApiRequestCache(1)]
        [WebApiRaw(HttpServerTools.JsonMime)]
        public ReadOnlyMemory<Byte> GetCpuChart() => GetCpuChart(CpuUsage);
        
        ReadOnlyMemory<Byte> GetCpuChart(double used, String title = "Cpu use")
        {
            double idle = 100.0 - used;
            var mem = String.Concat(used.ToString("0.00", CultureInfo.InvariantCulture), '%');
            return ChartJsService.ChartSerialize(new ChartJsConfig
            {
                RefreshRate = 2000,
                Title = title,
                type = "doughnut",
                Precision = 2,
                ValidTypes = ["doughnut"],
                ValueSuffix = " %",
                ValueLabel = 1,
                data = new ChartJsData
                {
                    labels = ["Use", "Idle"],
                    datasets = [
                        new ChartJsDataSet
                        {
                            data = [ used, idle ],
                            backgroundColor = DoughnutCpuColor,
                            borderWidth = 0,
                        }
                    ]
                },
                options = new ChartJsOptions
                {
                    plugins = new ChartJsPlugins
                    {
                        datalabels = new ChartJsDataLabels
                        {
                            display = true,
                        },
                        legend = new ChartJsLegend
                        {
                            display = false,
                        },
                        title = new ChartJsTitle
                        {
                            text = [title, mem],
                            display = true,
                        }

                    }
                }

            });
        }

        /// <summary>
        /// Get a historical chart for the Cpu memory use
        /// </summary>
        /// <returns>Graph data as json</returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        [WebApiClientCache(1)]
        [WebApiRequestCache(1)]
        [WebApiRaw(HttpServerTools.JsonMime)]
        public ReadOnlyMemory<Byte> GetCpuHistoryChart()
            => GetHistoryChart(CpuUsageHistory, x => x, "Cpu use last 24 hours", "Cpu use", "%", TimeSpan.FromHours(24), BarCpuColor, "HH:mm");


        /// <summary>
        /// Get a historical chart for the Cpu memory use
        /// </summary>
        /// <returns>Graph data as json</returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        [WebApiClientCache(1)]
        [WebApiRequestCache(1)]
        [WebApiRaw(HttpServerTools.JsonMime)]
        public ReadOnlyMemory<Byte> GetCpuHistoryShortChart()
            => GetHistoryChart(CpuUsageHistoryShort, x => x, "Cpu use last 3 minutes", "Cpu use", "%", TimeSpan.FromMinutes(3), BarCpuColor, "HH:mm:ss");


        readonly BucketValueHistory<double> CpuUsageHistory = new(TimeSpan.FromMinutes(15), TimeSpan.FromHours(24), (a, b) => a + b);
        readonly BucketValueHistory<double> CpuUsageHistoryShort = new(TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(3), (a, b) => a + b);


        #endregion//CPU info

        #region RAM info

        SmMemoryInfo MemInfo;

        /// <summary>
        /// Current system memory use and size
        /// </summary>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        [WebApiClientCache(1)]
        [WebApiRequestCache(1)]
        public SmMemoryInfo GetMemInfo() => MemInfo;

        /// <summary>
        /// Current system memory use chart
        /// </summary>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        [WebApiClientCache(1)]
        [WebApiRequestCache(1)]
        [WebApiRaw(HttpServerTools.JsonMime)]
        public ReadOnlyMemory<Byte> GetMemChart() => GetMemChart(MemInfo);
        
        ReadOnlyMemory<Byte> GetMemChart(SmMemoryInfo sm, String title = "Memory use")
        {
            if (sm == null)
                return null;
            return GetMemChart(sm.Free, sm.Total, sm.Used, title);
        }

        ReadOnlyMemory<Byte> GetMemChart(long freeBytes, long totalBytes, double usedPercentage, String title = "Memory use")
        {
            var freeGb = (double)((Decimal)freeBytes / GbSize);
            var usedGb = (double)((Decimal)(totalBytes - freeBytes) / GbSize);
            var mem = String.Concat(usedPercentage.ToString("0.00", CultureInfo.InvariantCulture), "% of ",
                (totalBytes / GbSize).ToString("### ### ##0.00", CultureInfo.InvariantCulture).TrimStart() + " GB");
            return ChartJsService.ChartSerialize(new ChartJsConfig
            {
                RefreshRate = 2000,
                Title = title,
                type = "doughnut",
                Precision = 2,
                ValidTypes = ["doughnut"],
                ValueSuffix = " GB",
                ValueLabel = 1,
                data = new ChartJsData
                {
                    labels = ["Used", "Free"],
                    datasets = [
                        new ChartJsDataSet
                        {
                            data = [ usedGb, freeGb ],
                            backgroundColor = DoughnutMemColor,
                            borderWidth = 0,
                        }
                    ]
                },
                options = new ChartJsOptions
                {
                    plugins = new ChartJsPlugins
                    {
                        datalabels = new ChartJsDataLabels
                        {
                            display = true,
                        },
                        legend = new ChartJsLegend
                        {
                            display = false,
                        },
                        title = new ChartJsTitle
                        {
                            text = [title, mem],
                            display = true,
                        }

                    }
                }

            });
        }

        /// <summary>
        /// Get a historical chart for the system memory use
        /// </summary>
        /// <returns>Graph data as json</returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        [WebApiClientCache(1)]
        [WebApiRequestCache(1)]
        [WebApiRaw(HttpServerTools.JsonMime)]
        public ReadOnlyMemory<Byte> GetMemHistoryChart()
            => GetHistoryChart(MemUsageHistory, x => x, "Memory use last 24 hours", "Memory use", "%", TimeSpan.FromHours(24), BarMemColor, "HH:mm");


        /// <summary>
        /// Get a historical chart for the system memory use
        /// </summary>
        /// <returns>Graph data as json</returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        [WebApiClientCache(1)]
        [WebApiRequestCache(1)]
        [WebApiRaw(HttpServerTools.JsonMime)]
        public ReadOnlyMemory<Byte> GetMemHistoryShortChart()
            => GetHistoryChart(MemUsageHistoryShort, x => x, "Memory use last 3 minutes", "Memory use", "%", TimeSpan.FromMinutes(3), BarMemColor, "HH:mm:ss");

        readonly BucketValueHistory<double> MemUsageHistory = new(TimeSpan.FromMinutes(15), TimeSpan.FromHours(24), (a, b) => a + b);
        readonly BucketValueHistory<double> MemUsageHistoryShort = new(TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(3), (a, b) => a + b);


        #endregion//RAM info

        #region Drive info

        SmDriveInfo[] DriveInfos;


        /// <summary>
        /// Information of the server drives
        /// </summary>
        /// <param name="r"></param>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        [WebMenuTable(null, "DriveInfo", "Drive information", "Information of the server drives", "../icons/ssd.svg", -4)]
        [WebApiClientCache(9)]
        [WebApiRequestCache(4)]
        public TableData DriveInfoTable(TableDataRequest r)
            => TableDataTools.Get(r, 10000, DriveInfos.Nullable());


        /// <summary>
        /// Get static server info
        /// </summary>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        [WebApiClientCacheStatic]
        [WebApiRequestCacheStatic]
        public SmServerInfo GetServerInfo()
        {
            var mi = MemInfo;
            return new SmServerInfo
            {
                ProcessorCount = Environment.ProcessorCount,
                Machine = Environment.MachineName,
                Os = Environment.OSVersion.VersionString,
                OsBase = EnvInfo.OsPlatform,
                DriveCount = DriveInfos?.Length ?? 0,
                ProcessCount = Processes?.Count ?? 0,
                Memory = mi?.Total ?? 0
            };
        }

        /// <summary>
        /// Get dynamic server stats
        /// </summary>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        [WebApiClientCache(4)]
        [WebApiRequestCache(3)]
        public SmServerStats GetServerStats()
        {
            return new SmServerStats
            {
                ProcessCount = Processes?.Count ?? 0,
            };
        }


        /// <summary>
        /// Get a chart for a single drive, start at 0 and increase until null is returned
        /// </summary>
        /// <param name="chartIndex"></param>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        [WebApiClientCache(9)]
        [WebApiRequestCache(4)]
        [WebApiRaw(HttpServerTools.JsonMime)]
        public ReadOnlyMemory<Byte> GetDriveChart(int chartIndex)
        {
            if (chartIndex < 0)
                return null;
            var t = DriveInfos;
            if (t == null)
                return null;
            if (chartIndex >= t.Length)
                return null;
            var drive = t[chartIndex];

            var f = drive.Free;
            var tot = drive.Total;
            var free = (double)((Decimal)f / GbSize);
            var used = (double)((Decimal)(tot - f) / GbSize);
            var title = String.Concat(drive.Drive, ' ', drive.Label);
            var mem = String.Concat(drive.Used.ToString("0.00", CultureInfo.InvariantCulture), "% of ",
                (tot / GbSize).ToString("### ### ##0.00", CultureInfo.InvariantCulture).TrimStart() + " GB");

            return ChartJsService.ChartSerialize(new ChartJsConfig
            {
                RefreshRate = 10000,
                Title = title,
                type = "doughnut",
                Precision = 1,
                ValidTypes = ["doughnut"],
                ValueSuffix = " GB",
                ValueLabel = 1,
                data = new ChartJsData
                {
                    labels = ["Used", "Free" ],
                    datasets = [
                        new ChartJsDataSet
                        {
                            data = [ used, free ],
                            backgroundColor = DoughnutDriveColor,
                            borderWidth = 0,
                        }
                    ]
                },
                options = new ChartJsOptions
                {
                    plugins = new ChartJsPlugins
                    {
                        datalabels = new ChartJsDataLabels
                        {
                            display = true,
                        },
                        legend = new ChartJsLegend
                        {
                            display = false,
                        },
                        title = new ChartJsTitle
                        {
                            text = [title, mem],
                            display = true,
                        }
                        
                    }
                }

            });

        }

        /// <summary>
        /// Get a historical chart for a single drive, start at 0 and increase until null is returned
        /// </summary>
        /// <param name="chartIndex"></param>
        /// <returns>Graph data as json</returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        [WebApiClientCache(9)]
        [WebApiRequestCache(4)]
        [WebApiRaw(HttpServerTools.JsonMime)]
        public ReadOnlyMemory<Byte> GetDriveHistoryChart(int chartIndex)
        {
            if (!DriveUsage.TryGetValue(chartIndex, out var h))
                return null;
            return GetHistoryChart(h.Item3, x => x, h.Item1 + " disc use last 90 days", "Disc use", "%", TimeSpan.FromDays(90), BarDriveColor, "MM-dd");
        }


        /// <summary>
        /// Get a historical chart for a single drive, start at 0 and increase until null is returned
        /// </summary>
        /// <param name="chartIndex"></param>
        /// <returns>Graph data as json</returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        [WebApiClientCache(9)]
        [WebApiRequestCache(4)]
        [WebApiRaw(HttpServerTools.JsonMime)]
        public ReadOnlyMemory<Byte> GetDriveHistoryShortChart(int chartIndex)
        {
            if (!DriveUsage.TryGetValue(chartIndex, out var h))
                return null;
            return GetHistoryChart(h.Item2, x => x, h.Item1 + " disc use last three days", "Disc use", "%", TimeSpan.FromDays(3), BarDriveColor, "MM-dd HH:mm");
        }


        #endregion //Drive info

        #region Services

        /// <summary>
        /// Current CPU use chart
        /// </summary>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        [WebApiClientCache(1)]
        [WebApiRequestCache(1)]
        [WebApiRaw(HttpServerTools.JsonMime)]
        public ReadOnlyMemory<Byte> GetServicesChart(HttpServerRequest context)
        {
            var d = GetServices(context);
            var running = d.Count(x => x.Status.FastEquals("Running"));
            var total = d.Length;
            var stopped = total - running;
            var title = "Running services";
            var stats = String.Concat(running, " / ", total);
            return ChartJsService.ChartSerialize(new ChartJsConfig
            {
                RefreshRate = 2000,
                Title = title,
                type = "doughnut",
                Precision = 1,
                ValidTypes = ["doughnut"],
                ValueLabel = 1,
                data = new ChartJsData
                {
                    labels = ["Running", "Stopped"],
                    datasets = [
                        new ChartJsDataSet
                        {
                            data = [ running, stopped ],
                            backgroundColor = DoughnutServiceColor,
                            borderWidth = 0,
                        }
                    ]
                },
                options = new ChartJsOptions
                {
                    plugins = new ChartJsPlugins
                    {
                        datalabels = new ChartJsDataLabels
                        {
                            display = true,
                        },
                        legend = new ChartJsLegend
                        {
                            display = false,
                        },
                        title = new ChartJsTitle
                        {
                            text = [title, stats],
                            display = true,
                        }

                    }
                }

            });
        }

        /// <summary>
        /// Get Memory graph for a managed service
        /// </summary>
        /// <param name="serviceName">Name of the service</param>
        /// <param name="context"></param>
        /// <returns>Graph data as json</returns>
        [WebApi]
        [WebApiAuth]
        [WebApiClientCache(4)]
        [WebApiRequestCache(3)]
        [WebApiRaw(HttpServerTools.JsonMime)]
        public ReadOnlyMemory<Byte> GetServiceMemChart(String serviceName, HttpServerRequest context)
        {
            var info = Validate(serviceName, context);
            var mi = MemInfo;
            if (mi == null)
                return null;
            var tot = mi.Total;
            var used = info.Process?.Metrics?.MemUsage ?? 0;
            double usedP = (double)(used * 100M / Math.Max(1M, tot));
            return GetMemChart(Math.Max(0, tot - used), tot, usedP, serviceName + " memory use");
        }


        /// <summary>
        /// Get Cpu graph for a managed service
        /// </summary>
        /// <param name="serviceName">Name of the service</param>
        /// <param name="context"></param>
        /// <returns>Graph data as json</returns>
        [WebApi]
        [WebApiAuth]
        [WebApiClientCache(4)]
        [WebApiRequestCache(3)]
        [WebApiRaw(HttpServerTools.JsonMime)]
        public ReadOnlyMemory<Byte> GetServiceCpuChart(String serviceName, HttpServerRequest context)
        {
            var info = Validate(serviceName, context);
            return GetCpuChart(info.Process?.Metrics.CpuUsage ?? 0, serviceName + " Cpu use");
        }


        /// <summary>
        /// Get Memory graph for a managed service
        /// </summary>
        /// <param name="serviceName">Name of the service</param>
        /// <param name="context"></param>
        /// <returns>Graph data as json</returns>
        [WebApi]
        [WebApiAuth]
        [WebApiClientCache(4)]
        [WebApiRequestCache(3)]
        [WebApiRaw(HttpServerTools.JsonMime)]
        public ReadOnlyMemory<Byte> GetServiceMemHistoryChart(String serviceName, HttpServerRequest context)
        {
            var info = Validate(serviceName, context);
            return GetHistoryChart(info.Process?.History, x => x.MemUsage, serviceName + " memory use last 24 hours", "Memory use", "Mb", TimeSpan.FromHours(24), BarMemColor, "HH:mm", 1.0 / (1024 * 1024), 2);
        }


        /// <summary>
        /// Get Cpu graph for a managed service
        /// </summary>
        /// <param name="serviceName">Name of the service</param>
        /// <param name="context"></param>
        /// <returns>Graph data as json</returns>
        [WebApi]
        [WebApiAuth]
        [WebApiClientCache(4)]
        [WebApiRequestCache(3)]
        [WebApiRaw(HttpServerTools.JsonMime)]
        public ReadOnlyMemory<Byte> GetServiceCpuHistoryChart(String serviceName, HttpServerRequest context)
        {
            var info = Validate(serviceName, context);
            return GetHistoryChart(info.Process?.History, x => x.CpuUsage, serviceName + " Cpu use last 24 hours", "Cpu use", "%", TimeSpan.FromHours(24), BarCpuColor, "HH:mm");
        }

        /// <summary>
        /// Get Memory graph for a managed service
        /// </summary>
        /// <param name="serviceName">Name of the service</param>
        /// <param name="context"></param>
        /// <returns>Graph data as json</returns>
        [WebApi]
        [WebApiAuth]
        [WebApiClientCache(4)]
        [WebApiRequestCache(3)]
        [WebApiRaw(HttpServerTools.JsonMime)]
        public ReadOnlyMemory<Byte> GetServiceMemHistoryShortChart(String serviceName, HttpServerRequest context)
        {
            var info = Validate(serviceName, context);
            return GetHistoryChart(info.Process?.HistoryShort, x => x.MemUsage, serviceName + " memory use last 3 minutes", "Memory use", "Mb", TimeSpan.FromMinutes(3), BarMemColor, "HH:mm:ss", 1.0 / (1024 * 1024), 2);
        }


        /// <summary>
        /// Get Cpu graph for a managed service
        /// </summary>
        /// <param name="serviceName">Name of the service</param>
        /// <param name="context"></param>
        /// <returns>Graph data as json</returns>
        [WebApi]
        [WebApiAuth]
        [WebApiClientCache(4)]
        [WebApiRequestCache(3)]
        [WebApiRaw(HttpServerTools.JsonMime)]
        public ReadOnlyMemory<Byte> GetServiceCpuHistoryShortChart(String serviceName, HttpServerRequest context)
        {
            var info = Validate(serviceName, context);
            return GetHistoryChart(info.Process?.HistoryShort, x => x.CpuUsage, serviceName + " Cpu use last 3 minutes", "Cpu use", "%", TimeSpan.FromMinutes(3), BarCpuColor, "HH:mm:ss");
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

        /// <summary>
        /// Get details of a managed service
        /// </summary>
        /// <param name="serviceName">Name of the managed service</param>
        /// <param name="context"></param>
        /// <returns>Details</returns>
        [WebApi]
        [WebApiAuth]
        [WebApiRequestCache(4)]
        public SmServiceDetail GetDetail(String serviceName, HttpServerRequest context)
            => InternalGetDetail(Validate(serviceName, context));

        SmServiceDetail InternalGetDetail(SmServiceInfo info)
        {
            var data = Syncer.GetFolderData(info.Service.Name).ToList();
            var discFolder = info.Syncher.DiscFolder;
            var exeName = FindServiceExe(discFolder);
            SmFileInfo[] configs = GetConfigs(discFolder, "*").Select(x => GetFileInfo(Path.Combine(discFolder, x))).OrderByDescending(x => x.LastModified).ToArray();

            SmFileInfo logFile = null;
            if (exeName != null)
            {
                exeName = Path.GetFileName(exeName);
                var baseName = Path.GetFileNameWithoutExtension(exeName);
                logFile = GetFileInfo(Path.Combine(discFolder, baseName + ".log"));
            }
            var masterFolder = Path.GetDirectoryName(discFolder);
            SmFileInfo[] masterConfigs = null;
            if (info.Service.MasterConfig)
                masterConfigs = GetConfigs(masterFolder, "*").Select(x => GetFileInfo(Path.Combine(masterFolder, x))).OrderByDescending(x => x.LastModified).ToArray();
            return new SmServiceDetail(info, data, exeName, logFile, configs, masterConfigs);
        }

        /// <summary>
        /// Get a list of all services that is managed
        /// </summary>
        /// <param name="context"></param>
        /// <returns>List of services</returns>
        [WebApi]
        [WebApiAuth]
        [WebApiRequestCache(4)]
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
            Interlocked.Exchange(ref info.Status, await CheckStatus(exe).ConfigureAwait(false));
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
        /// Kill the service process
        /// </summary>
        /// <param name="serviceName">Name of the managed service</param>
        /// <param name="context"></param>
        /// <returns>Updated service details</returns>
        [WebApi]
        [WebApiAuth]
        [WebApiAudit(AuditGroup)]
        public async Task<SmServiceDetail> Kill(String serviceName, HttpServerRequest context)
        {
            var info = Validate(serviceName, context);
            var pid = info.Process?.Id ?? 0;
            if (pid == 0)
                throw new Exception("The process isn't running!");
            Process h = Process.GetProcessById((int)pid);
            if (h == null)
                throw new Exception("Couldn't find a process with id " + pid);
            var exe = FindServiceExe(info.Syncher.DiscFolder);
            if (exe == null)
                return null;
            try
            {
                await RunCommand(exe.ToQuoted() + " stop").ConfigureAwait(false);
                try
                {
                    h = Process.GetProcessById((int)pid);
                }
                catch
                {
                    h = null;
                }
            }
            catch
            {
            }
            if (h != null)
            {
                Manager.AddMessage(LogPrefix + "Terminating process " + serviceName + ":" + pid + " and all child processes");
                h.Kill(true);
            }
            context.Session.InvalidateCache();
            context.Server.InvalidateCache();
            Interlocked.Exchange(ref info.Status, await CheckStatus(exe).ConfigureAwait(false));
            return InternalGetDetail(info);

        }

        /// <summary>
        /// All synched folders as a table
        /// </summary>
        /// <param name="r"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth]
        [WebApiRequestCache(4)]
        public TableData ServicesTable(TableDataRequest r, HttpServerRequest context)
        {
            var d = GetServices(context);
            if (!context.Session.IsValid(Auth.AuthTools.AdminAuth))
                return TableDataTools.Get(r, 5000, d);
            return TableDataTools.Get(r, 5000, d.Convert(x => new SmServiceBriefActions(x)));

        }


        #endregion//Services


        #region Storage

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
        [WebMenuTable(null, "ChunkStorage", "Chunk storage", "Analysis of the chunk storage", "../icons/brick.svg", -3)]
        [WebApiClientCache(14)]
        [WebApiRequestCache(10)]
        public async Task<TableData> StorageStatsTable(TableDataRequest r)
            => TableDataTools.Get(r, 15000, await GetStorageStats().ConfigureAwait(false));

        #endregion//Storage


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
        [WebApiRequestCache(4)]
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
                Interlocked.Exchange(ref info.Status, await CheckStatus(exe).ConfigureAwait(false));
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

        #region Configs

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
            if (!ServiceHost.IsConfigBackupName(sname, out var dname))
                throw new Exception("Config is not a backup!");
           
            sname = Path.Combine(bin, sname);
            dname = Path.Combine(bin, dname);
            if (!File.Exists(sname))
                throw new Exception("The configuration file does not exist!");
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
        /// Delete a config file.
        /// WARNING no backup is made!
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
            var masterBin = Path.GetDirectoryName(bin);
            if (data.IsMaster)
            {
                if (!info.Service.MasterConfig)
                    throw new Exception("Service is not configured to have master configs!");
                bin = masterBin;
            }
            var sname = data.Config;
            if (!IsValidConfigName(sname))
                throw new Exception("Invalid config name!");
            var fileName = sname;
            sname = Path.Combine(bin, sname);
            if (!File.Exists(sname))
                throw new Exception("The configuration file does not exist!");
            var bak = Path.Combine(masterBin, "bak");
            if (await PathExt.EnsureFolderExistAsync(bak).ConfigureAwait(false) == null)
            {
                var bakDest = Path.Combine(bak, fileName);
                if (File.Exists(bakDest))
                    ServiceHost.BackupConfig(bakDest, Manager);
                var ex = await PathExt.TryMoveFileAsync(sname, bakDest).ConfigureAwait(false);
                if (ex != null)
                    throw ex;
            }
            else
            {
                var ex = await PathExt.TryDeleteFileAsync(sname).ConfigureAwait(false);
                if (ex != null)
                    throw ex;
            }
            Syncer.GetFolderData(info.Syncher.Name);
            context.Session.InvalidateCache();
            context.Server.InvalidateCache();
            return true;
        }


        /// <summary>
        /// Update a config file
        /// </summary>
        /// <param name="data"></param>
        /// <param name="context"></param>
        /// <returns>True if sucessfully renamed</returns>
        /// <exception cref="Exception"></exception>
        [WebApi]
        [WebApiAuth]
        [WebApiAudit(AuditGroup)]
        public async Task<bool> UpdateConfig(EditSaveTextFile data, HttpServerRequest context)
        {
            var t = data.Url.Split('/');
            var tl = t.Length;
            var serviceName = t[tl - 2];
            bool isMaster = t[tl - 3].FastEquals("Data");
            var sname = t[tl - 1];

            var info = Validate(serviceName, context);
            var bin = info.Syncher.DiscFolder;
            if (isMaster)
            {
                if (!info.Service.MasterConfig)
                    throw new Exception("Service is not configured to have master configs!");
                bin = Path.GetDirectoryName(bin);
            }
            if (!IsValidConfigName(sname))
                throw new Exception("Invalid config name!");
            sname = Path.Combine(bin, sname);
            if (!File.Exists(sname))
                throw new Exception("The configuration file does not exist!");
            if (!ServiceHost.BackupConfig(sname, Manager))
                throw new Exception("Failed to backup exsiting file \"" + sname + "\"");
            await File.WriteAllTextAsync(sname, data.Content).ConfigureAwait(false);
            Syncer.GetFolderData(info.Syncher.Name);
            context.Session.InvalidateCache();
            context.Server.InvalidateCache();
            return true;
        }

        /// <summary>
        /// Delete a config file.
        /// WARNING no backup is made!
        /// </summary>
        /// <param name="data"></param>
        /// <param name="context"></param>
        /// <returns>True if sucessfully renamed</returns>
        /// <exception cref="Exception"></exception>
        [WebApi]
        [WebApiAuth]
        [WebApiAudit(AuditGroup)]
        public Task<bool> DeleteConfigFile(EditFile data, HttpServerRequest context)
        {
            var t = data.Url.Split('/');
            var tl = t.Length;
            var serviceName = t[tl - 2];
            bool isMaster = t[tl - 3].FastEquals("Data");
            var sname = t[tl - 1];
            return DeleteConfig(new SmConfigRequest
            {
                Config = sname,
                ServiceName = serviceName,
                IsMaster = isMaster,
            }, context);
        }

        #endregion//Configs

        #region Service management

        readonly Object ServiceLock = new object();

    /// <summary>
    /// Add a new managed service
    /// </summary>
    /// <param name="service">Service params</param>
    /// <returns>True if sucessfully added</returns>
    /// <exception cref="Exception"></exception>
    [WebApi]
    [WebApiAuth(Roles.Admin)]
    [WebApiAudit(AuditGroup)]
    public bool AddService(ManagedService service)
    {
        var name = service?.Name?.Trim();
        if (String.IsNullOrEmpty(name))
            throw new Exception("Invalid name! May not be empty or null!");
        if (!PathExt.IsValidFilename(name))
            throw new Exception("Invalid name! May only contain valid file name characters!");
        if (!PathExt.IsValidSubPath(name))
            throw new Exception("Invalid name! May only contain valid folder name characters!");
        service.Name = name;
        lock (ServiceLock)
        {
            InternalAddService(service);
            var savedServices = KeyValueStore.AllApp.TryGet<ManagedService[]>(ServerManagerServicesKey);
            savedServices = savedServices.Push(service);
            KeyValueStore.AllApp.Set(ServerManagerServicesKey, savedServices);
        }
        return true;
    }

    /// <summary>
    /// Remove a service from the Service Managers control.
    /// This will NOT stop, uninstall and remove the service from disc,
    /// </summary>
    /// <param name="serviceName">Name of the service</param>
    /// <returns>True if sucessfully removed</returns>
    /// <exception cref="Exception"></exception>
    [WebApi]
    [WebApiAuth(Roles.Admin)]
    [WebApiAudit(AuditGroup)]
    public bool RemoveService(String serviceName)
    {
        lock (ServiceLock)
        {
            if (!InternalRemoveService(serviceName))
                return false;
            var savedServices = KeyValueStore.AllApp.TryGet<ManagedService[]>(ServerManagerServicesKey);
            if (savedServices == null)
                return false;
            var i = savedServices.IndexOf(x => x.Name.FastEquals(serviceName));
            if (i < 0)
                return false;
            savedServices = savedServices.RemoveAt(i);
            KeyValueStore.AllApp.Set(ServerManagerServicesKey, savedServices.Length == 0 ? null : savedServices);
        }
        return true;
    }

    #endregion//Service management

        #region Keys

        /// <summary>
        /// Get a table of all key files found in the key folder
        /// </summary>
        /// <param name="r"></param>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth(Roles.Admin)]
        [WebApiRequestCache(4)]
        public TableData KeysTable(TableDataRequest r)
            => TableDataTools.Get(r, 5000, GetKeys());


        /// <summary>
        /// Get a list of all key files found in the key folder
        /// </summary>
        /// <returns>List of key files</returns>
        [WebApi]
        [WebApiAuth(Roles.Admin)]
        [WebApiRequestCache(4)]
        public SmKeyFile[] GetKeys()
        {
            List<SmKeyFile> files = new();
            if (Directory.Exists(KeyFolder))
            {
                foreach (var x in Directory.GetFiles(KeyFolder))
                {
                    var fi = new FileInfo(x);
                    var n = fi.Name;
                    bool bak = ServiceHost.IsConfigBackupName(n, out var o);
                    files.Add(new SmKeyFile
                    {
                        Name = n,
                        Size = fi.Length,
                        LastModified = fi.LastWriteTimeUtc,
                        Backup = bak
                    });
                }
            }
            return files.ToArray();
        }

        #endregion//Keys

        #region IHaveStats

        public IEnumerable<Stats> GetStats()
        {
            var sys = nameof(ServerManagerService);
            foreach (var x in StatusEx.GetStats(sys, "StatusExs."))
                yield return x;
            foreach (var x in DriveEx.GetStats(sys, "DriveExs."))
                yield return x;
        }

        #endregion//IHaveStats
    }


}
