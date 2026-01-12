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
            UpdateTask = new PeriodicTask(UpdateMetrics, 5000);
            UpdateStatsTask = new PeriodicTask(UpdateStats, 500);
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
                MemInfo = new SmMemoryInfo
                {
                    Free = (long)free,
                    Total = (long)tot,
                    Used = (double)((tot - free) * 100M / Math.Max(1M, tot))
                };
            }
            if (os.GetCpuUsage(out var cpu))
                CpuUsage = (float)cpu;
            return true;
        }

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


            var drives = DriveInfo.GetDrives();
            var dis = drives.Convert(x =>
            {
                var free = x.TotalFreeSpace;
                var tot = x.TotalSize;
                return new SmDriveInfo
                {
                    Drive = x.Name,
                    Label = x.VolumeLabel,
                    Format = x.DriveFormat,
                    Type = x.DriveType.ToString(),
                    Free = free,
                    Total = tot,
                    Used = (double)((tot - free) * 100M / Math.Max(1M, tot))
                };
            });
            DriveInfos = dis;

            return true;
        }

        const decimal GbSize = 1024M * 1024M * 1024M;

        #region CPU info

        float CpuUsage;

        /// <summary>
        /// Get the current CPU usage as a percentage
        /// </summary>
        /// <returns>[0, 100] current cpu usage</returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        [WebApiClientCache(1)]
        [WebApiRequestCache(1)]
        public float GetCpuUsage() => CpuUsage;


        /// <summary>
        /// Current CPU usage chart
        /// </summary>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        [WebApiClientCache(1)]
        [WebApiRequestCache(1)]
        [WebApiRaw(HttpServerTools.JsonMime)]
        public ReadOnlyMemory<Byte> GetCpuUsageChart()
        {
            double used = CpuUsage;
            double idle = 100.0 - used;
            var title = "CPU use";
            var mem = String.Concat(used.ToString("0.00", CultureInfo.InvariantCulture), '%');
            return ChartJsService.ChartSerialize(new ChartJsConfig
            {
                RefreshRate = 2000,
                Title = title,
                type = "doughnut",
                Precision = 1,
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
                            backgroundColor = ["#ff5566", "#881133" ],
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

        #endregion//CPU info


        #region RAM info

        SmMemoryInfo MemInfo;

        /// <summary>
        /// Current system memory usage and size
        /// </summary>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        [WebApiClientCache(1)]
        [WebApiRequestCache(1)]
        public SmMemoryInfo GetMemoryInfo() => MemInfo;

        /// <summary>
        /// Current system memory usage chart
        /// </summary>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        [WebApiClientCache(1)]
        [WebApiRequestCache(1)]
        [WebApiRaw(HttpServerTools.JsonMime)]
        public ReadOnlyMemory<Byte> GetMemoryChart()
        {
            var drive = MemInfo;
            if (drive == null)
                return null;

            var f = drive.Free;
            var tot = drive.Total;



            var free = (double)((Decimal)f / GbSize);
            var used = (double)((Decimal)(tot - f) / GbSize);
            var title = "Memory use";
            var mem = String.Concat(drive.Used.ToString("0.00", CultureInfo.InvariantCulture), "% of ",
                (tot / GbSize).ToString("### ### ##0.00", CultureInfo.InvariantCulture).TrimStart() + " GB");
            return ChartJsService.ChartSerialize(new ChartJsConfig
            {
                RefreshRate = 2000,
                Title = title,
                type = "doughnut",
                Precision = 1,
                ValidTypes = ["doughnut"],
                ValueSuffix = " GB",
                ValueLabel = 1,
                data = new ChartJsData
                {
                    labels = ["Used", "Free"],
                    datasets = [
                        new ChartJsDataSet
                        {
                            data = [ used, free ],
                            backgroundColor = ["#55ff66", "#118833" ],
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
        /// Get the number of drives
        /// </summary>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        [WebApiClientCache(9)]
        [WebApiRequestCache(4)]
        public int GetDriveCount()
        {
            var t = DriveInfos;
            if (t == null)
                return 0;
            return t.Length;
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
                            backgroundColor = ["#5566ff", "#113388" ],
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


        #endregion//Drive info



        SmServiceInfo Validate(String serviceName, HttpServerRequest context)
        {
            serviceName = serviceName.FastToLower();
            if (!Services.TryGetValue(serviceName, out var info))
                throw new Exception("Unknown service!");
            if (!context.Session.IsValid(info.Auth))
                throw new Exception("Not authorized!");
            return info;
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
            var pid = info.Metrics.ProcessHandle;
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
            info.Metrics.Status = await CheckStatus(exe).ConfigureAwait(false);
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
        [WebMenuTable(null, "ChunkStorage", "Chunk storage", "Analysis of the chunk storage", "../icons/brick.svg", -5)]
        [WebApiClientCache(14)]
        [WebApiRequestCache(10)]
        public async Task<TableData> StorageStatsTable(TableDataRequest r)
            => TableDataTools.Get(r, 15000, await GetStorageStats().ConfigureAwait(false));

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


    }

}
