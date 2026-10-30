using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SysWeaver.OsServices
{
    /// <summary>
    /// <see cref="ISystemServices"/> for systemd, uses "systemctl" to list (service units) and control services.
    /// Pause / continue uses freeze / thaw (systemd 246 and later, requires cgroup v2).
    /// </summary>
    sealed class SystemdSystemServices : ISystemServices
    {
        public String Name => "Linux systemd";

        public IReadOnlyList<OsServiceStartModes> StartModes { get; } = [OsServiceStartModes.Automatic, OsServiceStartModes.Manual, OsServiceStartModes.Disabled];

        readonly String Systemctl = UnixCommand.Find("systemctl") ?? "systemctl";

        /// <summary>
        /// The properties read for the list
        /// </summary>
        const String ListProps = "Id,Description,LoadState,ActiveState,SubState,UnitFileState,MainPID,ExecMainStatus,Type,User,FragmentPath,CanStart,CanStop,CanReload,CanFreeze,FreezerState,ExecStart";

        public IReadOnlyList<OsServiceInfo> GetServices()
        {
            var names = new SortedSet<String>(StringComparer.Ordinal);
            foreach (var line in Lines(UnixCommand.Get(Systemctl, "list-units", "--type=service", "--all", "--no-legend", "--no-pager", "--plain")))
            {
                var n = FirstWord(line);
                if (n.EndsWith(".service", StringComparison.Ordinal))
                    names.Add(n);
            }
            try
            {
                foreach (var line in Lines(UnixCommand.Get(Systemctl, "list-unit-files", "--type=service", "--no-legend", "--no-pager")))
                {
                    var n = FirstWord(line);
                    //  Templates ("x@.service") can't be started without an instance name
                    if (n.EndsWith(".service", StringComparison.Ordinal) && (!n.EndsWith("@.service", StringComparison.Ordinal)))
                        names.Add(n);
                }
            }
            catch
            {
            }
            var res = new List<OsServiceInfo>();
            var all = names.ToList();
            for (int i = 0; i < all.Count; i += 200)
            {
                var args = new List<String> { "show", "--no-pager", "-p", ListProps, "--" };
                args.AddRange(all.Skip(i).Take(200));
                foreach (var p in ParseBlocks(UnixCommand.Get(Systemctl, args.ToArray())))
                {
                    var x = ToInfo(p);
                    if (x != null)
                        res.Add(x);
                }
            }
            return res;
        }

        public OsServiceInfo GetService(String name)
        {
            name = Normalize(name);
            var p = ParseBlocks(UnixCommand.Get(Systemctl, "show", "--no-pager", "--", name)).FirstOrDefault();
            if (p == null)
                return null;
            var x = ToInfo(p);
            if (x == null)
                return null;
            String[] list(params String[] keys) => keys.SelectMany(k => p.TryGetValue(k, out var v) ? v.Split(' ', StringSplitOptions.RemoveEmptyEntries) : []).Distinct().ToArray();
            x.Dependencies = list("Requires", "Requisite", "Wants", "BindsTo");
            x.Dependents = list("RequiredBy", "RequisiteOf", "WantedBy", "BoundBy");
            x.Properties = p.Where(kv => kv.Value.Length > 0).OrderBy(kv => kv.Key, StringComparer.Ordinal).ToArray();
            return x;
        }

        static String Normalize(String name)
        {
            if (String.IsNullOrWhiteSpace(name))
                throw new ArgumentException("No service name!");
            name = name.Trim();
            return name.Contains('.') ? name : (name + ".service");
        }

        void Ctl(String what, String name, params String[] verbs)
        {
            name = Normalize(name);
            foreach (var v in verbs)
            {
                try
                {
                    UnixCommand.Change(Systemctl, v, "--", name);
                }
                catch (Exception ex)
                {
                    throw new Exception("Failed to " + what + " \"" + name + "\": " + ex.Message);
                }
            }
        }

        public void Start(String name) => Ctl("start", name, "start");

        public void Stop(String name) => Ctl("stop", name, "stop");

        public void Restart(String name) => Ctl("restart", name, "restart");

        public void Pause(String name) => Ctl("pause (freeze)", name, "freeze");

        public void Continue(String name) => Ctl("continue (thaw)", name, "thaw");

        public void Reload(String name) => Ctl("reload", name, "reload");

        public void SetStartMode(String name, OsServiceStartModes mode)
        {
            switch (mode)
            {
                case OsServiceStartModes.Automatic:
                    Ctl("enable", name, "unmask", "enable");
                    break;
                case OsServiceStartModes.Manual:
                    Ctl("disable", name, "unmask", "disable");
                    break;
                case OsServiceStartModes.Disabled:
                    Ctl("mask", name, "mask");
                    break;
                default:
                    throw new ArgumentException("The start mode " + mode + " can't be used with systemd!");
            }
        }

        public void Delete(String name)
            => throw new NotSupportedException("systemd units can't be deleted, disable (mask) it instead!");

        #region Parsing

        internal static IEnumerable<String> Lines(String text)
            => (text ?? "").Split('\n').Select(x => x.TrimEnd('\r')).Where(x => x.Trim().Length > 0);

        static String FirstWord(String line)
        {
            line = line.Trim();
            //  Some versions prefix with a status bullet
            if (line.StartsWith('●') || line.StartsWith('*'))
                line = line.Substring(1).Trim();
            var i = line.IndexOf(' ');
            return i < 0 ? line : line.Substring(0, i);
        }

        /// <summary>
        /// Parse the output of "systemctl show" (key=value lines, units separated by an empty line)
        /// </summary>
        static List<Dictionary<String, String>> ParseBlocks(String text)
        {
            var res = new List<Dictionary<String, String>>();
            Dictionary<String, String> cur = null;
            foreach (var raw in (text ?? "").Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (line.Length <= 0)
                {
                    cur = null;
                    continue;
                }
                var i = line.IndexOf('=');
                if (i <= 0)
                    continue;
                if (cur == null)
                {
                    cur = new Dictionary<String, String>(StringComparer.Ordinal);
                    res.Add(cur);
                }
                cur[line.Substring(0, i)] = line.Substring(i + 1);
            }
            return res;
        }

        static OsServiceInfo ToInfo(Dictionary<String, String> p)
        {
            String g(String key) => p.TryGetValue(key, out var v) ? v : "";
            var id = g("Id");
            var load = g("LoadState");
            if ((id.Length <= 0) || (load == "not-found"))
                return null;
            var active = g("ActiveState");
            var sub = g("SubState");
            var freezer = g("FreezerState");
            var unitFile = g("UnitFileState");
            var x = new OsServiceInfo
            {
                Name = id,
                DisplayName = id.EndsWith(".service", StringComparison.Ordinal) ? id.Substring(0, id.Length - 8) : id,
                Description = g("Description"),
                StatusText = sub.Length > 0 ? String.Concat(active, " (", sub, ")") : active,
                ServiceType = g("Type"),
                Account = g("User"),
                File = g("FragmentPath"),
                Command = ExecCommand(g("ExecStart")),
                StartModeText = load == "masked" ? "masked" : unitFile,
            };
            if (x.Account.Length <= 0)
                x.Account = "root";
            if (int.TryParse(g("MainPID"), out var pid))
                x.ProcessId = pid;
            if (int.TryParse(g("ExecMainStatus"), out var ec))
                x.ExitCode = ec;
            x.Status = active switch
            {
                "active" => freezer == "frozen" ? ServiceStatus.Paused : ServiceStatus.Running,
                "reloading" => ServiceStatus.Running,
                "refreshing" => ServiceStatus.Running,
                "activating" => ServiceStatus.StartPending,
                "deactivating" => ServiceStatus.StopPending,
                "inactive" => ServiceStatus.Stopped,
                "failed" => ServiceStatus.Stopped,
                "maintenance" => ServiceStatus.Stopped,
                _ => ServiceStatus.Unknown,
            };
            if (freezer is "freezing")
                x.Status = ServiceStatus.PausePending;
            else if (freezer is "thawing")
                x.Status = ServiceStatus.ContinuePending;
            x.StartMode = (load == "masked") ? OsServiceStartModes.Disabled : unitFile switch
            {
                "enabled" or "enabled-runtime" => OsServiceStartModes.Automatic,
                "disabled" => OsServiceStartModes.Manual,
                "masked" or "masked-runtime" => OsServiceStartModes.Disabled,
                "" => OsServiceStartModes.Unknown,
                _ => OsServiceStartModes.Static,
            };
            bool yes(String key) => g(key) == "yes";
            var a = OsServiceActions.None;
            var isActive = x.Status is ServiceStatus.Running or ServiceStatus.Paused;
            if ((x.Status == ServiceStatus.Stopped) && yes("CanStart") && (x.StartMode != OsServiceStartModes.Disabled))
                a |= OsServiceActions.Start;
            if (isActive && yes("CanStop"))
                a |= OsServiceActions.Stop;
            if (isActive && yes("CanStart") && yes("CanStop"))
                a |= OsServiceActions.Restart;
            if ((x.Status == ServiceStatus.Running) && yes("CanReload"))
                a |= OsServiceActions.Reload;
            if ((x.Status == ServiceStatus.Running) && yes("CanFreeze"))
                a |= OsServiceActions.Pause;
            if (x.Status == ServiceStatus.Paused)
                a |= OsServiceActions.Continue;
            if ((x.StartMode is OsServiceStartModes.Automatic or OsServiceStartModes.Manual or OsServiceStartModes.Disabled) || (unitFile.Length > 0))
                a |= OsServiceActions.SetStartMode;
            x.Actions = a;
            return x;
        }

        /// <summary>
        /// Get the command line from an ExecStart value: "{ path=/usr/sbin/sshd ; argv[]=/usr/sbin/sshd -D $SSHD_OPTS ; ignore_errors=no ; .. }"
        /// </summary>
        static String ExecCommand(String exec)
        {
            if (String.IsNullOrEmpty(exec))
                return "";
            var i = exec.IndexOf("argv[]=", StringComparison.Ordinal);
            if (i < 0)
                return exec.Trim('{', '}', ' ');
            i += 7;
            var e = exec.IndexOf(" ;", i, StringComparison.Ordinal);
            return (e < 0 ? exec.Substring(i) : exec.Substring(i, e - i)).Trim();
        }

        #endregion//Parsing
    }


    /// <summary>
    /// <see cref="ISystemServices"/> for SysVinit, the services are the scripts in /etc/init.d (controlled using "service" or the script directly).
    /// The status is the exit code of the "status" command (LSB: 0 = running, 3 = stopped).
    /// </summary>
    sealed class SysVinitSystemServices : ISystemServices
    {
        public String Name => "Linux SysVinit";

        public IReadOnlyList<OsServiceStartModes> StartModes { get; } = [OsServiceStartModes.Automatic, OsServiceStartModes.Manual];

        const String InitD = "/etc/init.d";

        static readonly HashSet<String> Skip = new(StringComparer.Ordinal) { "README", "skeleton", "rc", "rcS", "rc.local", "functions", "halt", "reboot", "single", "killprocs", "sendsigs" };

        static String Script(String name)
        {
            if (String.IsNullOrWhiteSpace(name) || name.Contains('/') || name.StartsWith('.'))
                throw new ArgumentException("Invalid service name \"" + name + "\"!");
            var f = Path.Combine(InitD, name.Trim());
            if (!File.Exists(f))
                throw new Exception("The service \"" + name + "\" doesn't exist!");
            return f;
        }

        static bool IsEnabled(String name)
        {
            foreach (var l in "2345")
            {
                var d = "/etc/rc" + l + ".d";
                try
                {
                    if (Directory.Exists(d) && Directory.EnumerateFiles(d, "S??" + name).Any())
                        return true;
                }
                catch
                {
                }
            }
            return false;
        }

        /// <summary>
        /// Read the LSB header "# Short-Description:" / "# Description:" of a script
        /// </summary>
        static String Describe(String file)
        {
            try
            {
                String desc = null;
                foreach (var line in File.ReadLines(file).Take(60))
                {
                    if (line.StartsWith("# Short-Description:", StringComparison.Ordinal))
                        return line.Substring(20).Trim();
                    if ((desc == null) && line.StartsWith("# Description:", StringComparison.Ordinal))
                        desc = line.Substring(14).Trim();
                }
                return desc ?? "";
            }
            catch
            {
                return "";
            }
        }

        static OsServiceInfo Read(String file)
        {
            var name = Path.GetFileName(file);
            var r = UnixCommand.Run(out var o, out _, file, ["status"]);
            var status = r switch
            {
                0 => ServiceStatus.Running,
                1 or 2 or 3 => ServiceStatus.Stopped,
                _ => ServiceStatus.Unknown,
            };
            var enabled = IsEnabled(name);
            var x = new OsServiceInfo
            {
                Name = name,
                DisplayName = name,
                Description = Describe(file),
                Status = status,
                StatusText = (o ?? "").Trim().Split('\n').FirstOrDefault() ?? "",
                StartMode = enabled ? OsServiceStartModes.Automatic : OsServiceStartModes.Manual,
                StartModeText = enabled ? "enabled" : "disabled",
                ServiceType = "init script",
                Account = "",
                Command = file,
                File = file,
                ExitCode = r,
            };
            var a = OsServiceActions.SetStartMode | OsServiceActions.Restart;
            if (status != ServiceStatus.Running)
                a |= OsServiceActions.Start;
            if (status != ServiceStatus.Stopped)
                a |= OsServiceActions.Stop;
            x.Actions = a;
            return x;
        }

        public IReadOnlyList<OsServiceInfo> GetServices()
        {
            if (!Directory.Exists(InitD))
                return [];
            return Directory.EnumerateFiles(InitD)
                .Where(f => !Skip.Contains(Path.GetFileName(f)) && !Path.GetFileName(f).StartsWith('.'))
                .OrderBy(f => f, StringComparer.Ordinal)
                .AsParallel().AsOrdered()
                .Select(Read)
                .ToList();
        }

        public OsServiceInfo GetService(String name)
        {
            String f;
            try
            {
                f = Script(name);
            }
            catch
            {
                return null;
            }
            var x = Read(f);
            x.Dependencies = [];
            x.Dependents = [];
            var props = new List<KeyValuePair<String, String>>();
            try
            {
                foreach (var line in File.ReadLines(f).Take(60))
                {
                    if (line.StartsWith("# ", StringComparison.Ordinal) && line.Contains(':'))
                    {
                        var i = line.IndexOf(':');
                        var k = line.Substring(2, i - 2).Trim();
                        var v = line.Substring(i + 1).Trim();
                        if ((k.Length > 0) && (k.Length < 40) && (v.Length > 0))
                            props.Add(new(k, v));
                        if (k == "Required-Start")
                            x.Dependencies = v.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    }
                }
            }
            catch
            {
            }
            x.Properties = props.ToArray();
            return x;
        }

        static void Do(String what, String name, String verb)
        {
            var f = Script(name);
            try
            {
                UnixCommand.Change(f, verb);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to " + what + " \"" + name + "\": " + ex.Message);
            }
        }

        public void Start(String name) => Do("start", name, "start");

        public void Stop(String name) => Do("stop", name, "stop");

        public void Restart(String name) => Do("restart", name, "restart");

        public void Pause(String name) => throw new NotSupportedException("SysVinit services can't be paused!");

        public void Continue(String name) => throw new NotSupportedException("SysVinit services can't be paused!");

        public void Reload(String name) => Do("reload", name, "reload");

        public void SetStartMode(String name, OsServiceStartModes mode)
        {
            Script(name);
            bool enable = mode switch
            {
                OsServiceStartModes.Automatic => true,
                OsServiceStartModes.Manual => false,
                _ => throw new ArgumentException("The start mode " + mode + " can't be used with SysVinit!"),
            };
            var updateRc = UnixCommand.Find("update-rc.d");
            if (updateRc != null)
            {
                UnixCommand.Change(updateRc, name, enable ? "defaults" : "remove");
                if (enable)
                    UnixCommand.Change(updateRc, name, "enable");
                return;
            }
            var chkconfig = UnixCommand.Find("chkconfig") ?? throw new Exception("Neither \"update-rc.d\" nor \"chkconfig\" was found!");
            UnixCommand.Change(chkconfig, name, enable ? "on" : "off");
        }

        public void Delete(String name) => throw new NotSupportedException("SysVinit services can't be deleted!");
    }
}
