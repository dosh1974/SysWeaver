using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Net;
using SysWeaver.OsServices;

namespace SysWeaver.MicroService
{

    /// <summary>
    /// The command line interface that a managed service executable must implement (the SysWeaver.OsServices verbs and exit codes, see it's README),
    /// any language / runtime can be used.
    /// </summary>
    public static class SmServiceCli
    {
        /// <summary>
        /// The documented exit codes of the "status" verb: 0 = running, 3 = stopped, 4 = unknown, 150 + the status for the others.
        /// </summary>
        static readonly HashSet<int> StatusExitCodes = [0, 3, 4, 151, 153, 154, 156, 157, 158];

        /// <summary>
        /// The documented exit codes of the other verbs that are specific to the service interface (1 and 2 are generic errors that any program can return):
        /// 0 = ok, 3 = not supported, 5 = not installed, 7 = not running, 150-155 = install / uninstall / start / stop / pause / continue failed.
        /// </summary>
        static readonly HashSet<int> VerbExitCodes = [0, 3, 5, 7, 150, 151, 152, 153, 154, 155];

        /// <summary>
        /// Test if the result of running a verb is the result of an executable that implements the service interface (as opposed to some other program, or a program that couldn't be run).
        /// A result is expected if the process ran and exited with a documented exit code, or printed a result line ("... [Name: Value]", see <see cref="ServiceResponseHelper.TryParseOutput"/>).
        /// A time out is never "unexpected" here (the right executable can hang, that's handled by the caller).
        /// </summary>
        /// <param name="verb">The verb, ex: "start", "status"</param>
        /// <param name="exited">True if the process ran and exited (by itself)</param>
        /// <param name="exitCode">The exit code</param>
        /// <param name="output">The output</param>
        /// <returns>True if the result is expected</returns>
        public static bool IsExpected(String verb, bool exited, int exitCode, String output)
        {
            if (!exited)
                return false;
            if (ServiceResponseHelper.TryParseOutput(output, out var _))
                return true;
            if (verb.FastEquals("status") && HasStatusLine(output))
                return true;
            return (verb.FastEquals("status") ? StatusExitCodes : VerbExitCodes).Contains(exitCode);
        }

        /// <summary>
        /// Test if the result of running "status" proves that an executable implements the service interface (used before switching to another executable):
        /// a result line or a status line is printed, or a documented status exit code other than 0 is returned (0 is too common to prove anything).
        /// </summary>
        /// <param name="exited">True if the process ran and exited (by itself)</param>
        /// <param name="exitCode">The exit code</param>
        /// <param name="output">The output</param>
        /// <returns>True if it's a service status response</returns>
        public static bool IsServiceStatusResponse(bool exited, int exitCode, String output)
        {
            if (!exited)
                return false;
            if (ServiceResponseHelper.TryParseOutput(output, out var _) || HasStatusLine(output))
                return true;
            return (exitCode != 0) && StatusExitCodes.Contains(exitCode);
        }

        static bool HasStatusLine(String output) => TryParseStatusLine(output, out var _);

        /// <summary>
        /// Find the status in a "Checking status: ... [Name: Value]" line.
        /// </summary>
        static bool TryParseStatusLine(String output, out ServiceStatus status)
        {
            status = ServiceStatus.Unknown;
            if (String.IsNullOrEmpty(output))
                return false;
            foreach (var lx in output.Split('\n'))
            {
                var l = lx.Trim();
                if (!l.FastStartsWith("Checking status:"))
                    continue;
                var s = l.LastIndexOf(' ') + 1;
                var e = l.LastIndexOf(']');
                if (e <= s)
                    continue;
                if (!int.TryParse(l.AsSpan(s, e - s), System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out var v))
                    continue;
                status = (ServiceStatus)v;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Get the status from the result of running the "status" verb: the status line if printed, else the exit code (see <see cref="ServiceStatusHelper.FromExitCode"/>).
        /// </summary>
        /// <param name="exited">True if the process ran and exited (by itself)</param>
        /// <param name="exitCode">The exit code</param>
        /// <param name="output">The output</param>
        /// <returns>The status, <see cref="ServiceStatus.Unknown"/> if it can't be determined</returns>
        public static ServiceStatus ParseStatus(bool exited, int exitCode, String output)
        {
            if (!exited)
                return ServiceStatus.Unknown;
            if (TryParseStatusLine(output, out var s))
                return s;
            return ServiceStatusHelper.FromExitCode(exitCode);
        }

        /// <summary>
        /// Get the exit code of a verb independent of the exit code convention of the service host version (older versions exited with the <see cref="ServiceResponse"/> value):
        /// the printed result is used if found.
        /// </summary>
        /// <param name="verb">The verb</param>
        /// <param name="exited">True if the process ran and exited (by itself)</param>
        /// <param name="timedOut">True if the process timed out (and was killed)</param>
        /// <param name="exitCode">The exit code</param>
        /// <param name="output">The output</param>
        /// <returns>The exit code (0 = success), 124 if timed out, -42 if it couldn't be run</returns>
        public static int GetExitCode(String verb, bool exited, bool timedOut, int exitCode, String output)
        {
            if (!exited)
                return timedOut ? 124 : -42;
            if (ServiceResponseHelper.TryParseOutput(output, out var response) && Enum.TryParse<ServiceVerbs>(verb, true, out var v))
                return response.ToExitCode(v);
            return exitCode;
        }
    }


    /// <summary>
    /// Linux process matching helpers (the /proc file system).
    /// </summary>
    public static class SmProcessMatch
    {
        /// <summary>
        /// Read a /proc/[pid]/[name] file, null if it can't be read.
        /// </summary>
        public static String ReadProc(int pid, String name)
        {
            try
            {
                return File.ReadAllText("/proc/" + pid + "/" + name);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the arguments of a process (including the executable), NUL separated, null if it can't be read.
        /// Linux: /proc/[pid]/cmdline, Windows: the command line of the process (NtQueryInformationProcess) split with CommandLineToArgvW.
        /// </summary>
        public static String GetCommandLine(int pid)
        {
            if (OperatingSystem.IsWindows())
                return WinCommandLine.Get(pid);
            return ReadProc(pid, "cmdline");
        }

        /// <summary>
        /// Get the first argument of a command line that is inside a folder.
        /// Only fully qualified paths are tested (normalized, so "/" on Windows and ".." works).
        /// </summary>
        /// <param name="cmdline">The NUL separated arguments, see <see cref="GetCommandLine(int)"/></param>
        /// <param name="dir">The full path of the folder, ending with a directory separator</param>
        /// <param name="ignoreCase">True to compare without case (Windows)</param>
        /// <returns>The argument or null</returns>
        public static String GetArgInFolder(String cmdline, String dir, bool ignoreCase = false)
        {
            if (String.IsNullOrEmpty(cmdline))
                return null;
            var cmp = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            foreach (var a in cmdline.Split('\0'))
            {
                if (a.Length == 0)
                    continue;
                var t = a;
                try
                {
                    if (Path.IsPathFullyQualified(t))
                        t = Path.GetFullPath(t);
                }
                catch
                {
                }
                if (t.StartsWith(dir, cmp))
                    return a;
            }
            return null;
        }

        /// <summary>
        /// Reads the command line of another process on Windows (needs only PROCESS_QUERY_LIMITED_INFORMATION, works for processes of other users when elevated).
        /// </summary>
        static class WinCommandLine
        {
            const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
            const int ProcessCommandLineInformation = 60;
            const int STATUS_INFO_LENGTH_MISMATCH = unchecked((int)0xC0000004);

            [DllImport("kernel32.dll", SetLastError = true)]
            static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

            [DllImport("kernel32.dll")]
            static extern bool CloseHandle(IntPtr h);

            [DllImport("ntdll.dll")]
            static extern int NtQueryInformationProcess(IntPtr process, int infoClass, IntPtr info, int infoLength, out int returnLength);

            [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
            static extern IntPtr CommandLineToArgvW(String cmdLine, out int argc);

            [DllImport("kernel32.dll")]
            static extern IntPtr LocalFree(IntPtr mem);

            public static String Get(int pid)
            {
                var h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
                if (h == IntPtr.Zero)
                    return null;
                try
                {
                    var st = NtQueryInformationProcess(h, ProcessCommandLineInformation, IntPtr.Zero, 0, out var len);
                    if ((st != STATUS_INFO_LENGTH_MISMATCH) || (len <= 0) || (len > (1 << 20)))
                        return null;
                    var buf = Marshal.AllocHGlobal(len);
                    try
                    {
                        if (NtQueryInformationProcess(h, ProcessCommandLineInformation, buf, len, out _) != 0)
                            return null;
                        //  UNICODE_STRING: ushort Length (bytes), ushort MaximumLength, pointer Buffer (aligned)
                        var bytes = (ushort)Marshal.ReadInt16(buf);
                        var str = Marshal.ReadIntPtr(buf, IntPtr.Size);
                        if ((bytes == 0) || (str == IntPtr.Zero))
                            return null;
                        var cmd = Marshal.PtrToStringUni(str, bytes / 2);
                        var argv = CommandLineToArgvW(cmd, out var argc);
                        if (argv == IntPtr.Zero)
                            return cmd;
                        try
                        {
                            var a = new String[argc];
                            for (int i = 0; i < argc; ++i)
                                a[i] = Marshal.PtrToStringUni(Marshal.ReadIntPtr(argv, i * IntPtr.Size));
                            return String.Join('\0', a);
                        }
                        finally
                        {
                            LocalFree(argv);
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(buf);
                    }
                }
                catch
                {
                    return null;
                }
                finally
                {
                    CloseHandle(h);
                }
            }
        }

        /// <summary>
        /// Get the systemd system service unit of a process from the content of /proc/[pid]/cgroup (the last "*.service" in the cgroup path).
        /// Processes of a user manager (user@N.service) or in a scope (a login session) returns null.
        /// </summary>
        /// <param name="cgroup">The content of /proc/[pid]/cgroup</param>
        /// <returns>The unit name, ex: "myservice.service", or null</returns>
        public static String GetServiceUnit(String cgroup)
        {
            if (String.IsNullOrEmpty(cgroup))
                return null;
            String path = null;
            foreach (var line in cgroup.Split('\n'))
            {
                //  hierarchy-ID:controller-list:cgroup-path, v2 = "0::/system.slice/x.service", v1 = "1:name=systemd:/system.slice/x.service"
                var a = line.IndexOf(':');
                if (a < 0)
                    continue;
                var b = line.IndexOf(':', a + 1);
                if (b < 0)
                    continue;
                var ctrl = line.Substring(a + 1, b - a - 1);
                if ((ctrl.Length == 0) || (ctrl == "name=systemd"))
                {
                    path = line.Substring(b + 1).Trim();
                    if (ctrl == "name=systemd")
                        break;
                }
            }
            if (path == null)
                return null;
            String unit = null;
            foreach (var p in path.Split('/'))
            {
                if (p.StartsWith("user@", StringComparison.Ordinal))
                    return null;
                if (p.EndsWith(".service", StringComparison.Ordinal) && (p.Length > 8))
                    unit = p;
            }
            return unit;
        }
    }


    public sealed partial class ServerManagerService
    {
        const String ExeKeyPrefix = "ServerManagerExe_";

        static String ExeKey(String serviceName) => ExeKeyPrefix + serviceName.FastToLower();

        static readonly StringComparison PathCmp = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        static String TrimFolder(String folder) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));

        /// <summary>
        /// The executable list of the active folder is re-scanned after this time (or when the folder's last write time changes).
        /// </summary>
        static readonly TimeSpan CandidateCacheTime = TimeSpan.FromMinutes(1);

        /// <summary>
        /// A folder without any executable is re-scanned after this time.
        /// </summary>
        static readonly TimeSpan MissingExeRetry = TimeSpan.FromSeconds(30);

        /// <summary>
        /// The minimum time between two attempts to fall back to another executable.
        /// </summary>
        static readonly TimeSpan ExeFallbackInterval = TimeSpan.FromMinutes(5);

        /// <summary>
        /// The time out of the "status" verb.
        /// </summary>
        static readonly TimeSpan StatusTimeout = TimeSpan.FromSeconds(15);

        TimeSpan VerbTimeout => TimeSpan.FromSeconds(Math.Max(5, P.VerbTimeoutSeconds));

        TimeSpan SwapStopTimeout => TimeSpan.FromSeconds(Math.Max(1, P.SwapStopTimeoutSeconds));

        /// <summary>
        /// Get the executables in a folder, cached for the active folder of the service.
        /// </summary>
        IReadOnlyList<ExecutableFile> GetCandidates(SmServiceInfo info, String folder = null)
        {
            var active = TrimFolder(info.Syncher.DiscFolder);
            folder = folder == null ? active : TrimFolder(folder);
            if (!String.Equals(folder, active, PathCmp))
                return PlatformTools.Current.FindExecutables(folder);
            DateTime ft = default;
            try
            {
                ft = Directory.GetLastWriteTimeUtc(folder);
            }
            catch
            {
            }
            var now = DateTime.UtcNow;
            var c = info.Candidates;
            if ((c != null) && (c.FolderTime == ft) && ((now - c.Scanned) < CandidateCacheTime) && String.Equals(c.Folder, folder, PathCmp))
                return c.Files;
            var files = PlatformTools.Current.FindExecutables(folder);
            info.Candidates = new SmExeCandidates
            {
                Folder = folder,
                FolderTime = ft,
                Scanned = now,
                Files = files,
            };
            return files;
        }

        /// <summary>
        /// Forget the cached executable and candidates (ex: after a version was activated), the persisted selection is kept.
        /// </summary>
        static void InvalidateExe(SmServiceInfo info)
        {
            info.Candidates = null;
            info.Exe = null;
        }

        /// <summary>
        /// Get the executable of a managed service (full path), null if there is none.
        /// The persisted selection (see <see cref="SmExeSelection"/>) is used as long as the file exists, else the best guess is used (and persisted).
        /// </summary>
        String GetServiceExe(SmServiceInfo info)
        {
            var s = info.Exe;
            if (s != null)
            {
                if (s.FullName != null)
                {
                    if (File.Exists(s.FullName))
                        return s.FullName;
                }
                else
                {
                    if ((DateTime.UtcNow - s.Resolved) < MissingExeRetry)
                        return null;
                }
            }
            lock (info.ExeLock)
            {
                s = info.Exe;
                if ((s?.FullName != null) && File.Exists(s.FullName))
                    return s.FullName;
                return ResolveExe(info).FullName;
            }
        }

        /// <summary>
        /// Resolve the executable (call with the ExeLock held).
        /// </summary>
        SmExeState ResolveExe(SmServiceInfo info)
        {
            var name = info.Service.Name;
            var folder = TrimFolder(info.Syncher.DiscFolder);
            var key = ExeKey(name);
            var store = KeyValueStore.AllApp;
            var sel = store.TryGet<SmExeSelection>(key);
            String full = null;
            bool manual = false;
            if ((sel != null) && IsPlainFileName(sel.Name))
            {
                var f = Path.Combine(folder, sel.Name);
                if (File.Exists(f))
                {
                    full = f;
                    manual = sel.Manual;
                }
            }
            if (full == null)
            {
                var c = GetCandidates(info);
                if (c.Count > 0)
                {
                    var best = c[0];
                    full = best.FullName;
                    if (sel?.Manual ?? false)
                    {
                        //  Keep the user's selection (it may come back with another version), use the best guess meanwhile
                        Manager.AddMessage(MessageLevels.Warning, $"{LogPrefix}The selected executable \"{sel.Name}\" of service \"{name}\" doesn't exist, using \"{best.Name}\" (detected)");
                    }
                    else
                    {
                        store.Set(key, new SmExeSelection { Name = best.Name, Manual = false });
                        Manager.AddMessage(MessageLevels.Info, $"{LogPrefix}Using \"{best.Name}\" as the executable of service \"{name}\" (detected, {c.Count} candidate{(c.Count == 1 ? "" : "s")})");
                    }
                }
            }
            var s = new SmExeState
            {
                FullName = full,
                Manual = manual,
                Resolved = DateTime.UtcNow,
            };
            info.Exe = s;
            return s;
        }

        /// <summary>
        /// Find the executable of a service in another folder than the active one (ex: a new version): the current executable name if it exists there, else the best guess.
        /// </summary>
        String FindExeIn(SmServiceInfo info, String folder)
        {
            folder = TrimFolder(folder);
            if (String.Equals(folder, TrimFolder(info.Syncher.DiscFolder), PathCmp))
                return GetServiceExe(info);
            var current = info.Exe?.FullName;
            var name = current != null ? Path.GetFileName(current) : KeyValueStore.AllApp.TryGet<SmExeSelection>(ExeKey(info.Service.Name))?.Name;
            if (IsPlainFileName(name))
            {
                var f = Path.Combine(folder, name);
                if (File.Exists(f))
                    return f;
            }
            var c = PlatformTools.Current.FindExecutables(folder);
            return c.Count > 0 ? c[0].FullName : null;
        }

        /// <summary>
        /// Run a verb using a specific executable, with a time out (the verb process tree is killed on time out, not the service).
        /// </summary>
        async Task<ExternalProcessResult> RunExeVerb(String exe, String verb, TimeSpan timeout, bool log = true)
        {
            var m = Manager;
            if (log)
                m.AddMessage(MessageLevels.Info, $"{LogPrefix}Executing: {exe.ToQuoted()} {verb}");
            ExternalProcessResult r;
            Action<String, bool> onMessage = null;
            if (log)
                onMessage = (text, err) => m.AddMessage(LogPrefix + text, err ? MessageLevels.Warning : MessageLevels.Info);
            using (log ? m.Tab() : null)
            {
                r = await ExternalProcess.RunWithTimeoutAsync(exe, [verb], timeout, null, onMessage).ConfigureAwait(false);
            }
            if (r.TimedOut)
                m.AddMessage(MessageLevels.Warning, $"{LogPrefix}{exe.ToQuoted()} {verb} didn't finish within {timeout.TotalSeconds} seconds and was killed");
            else if (r.Exception != null)
                m.AddMessage(LogPrefix + "Failed to run " + exe.ToQuoted() + " " + verb, r.Exception, MessageLevels.Warning);
            return r;
        }

        /// <summary>
        /// Run a verb on a managed service using it's executable.
        /// If the result is unexpected (the executable couldn't be run, or the exit code / output isn't from the service interface, see <see cref="SmServiceCli.IsExpected"/>),
        /// and the executable wasn't selected by a user, the other executables are probed and the verb is run again with the first one that responds as a service.
        /// </summary>
        /// <returns>The result, null if the service has no executable</returns>
        async Task<ExternalProcessResult> RunServiceVerb(SmServiceInfo info, String verb, TimeSpan timeout, bool log = true)
        {
            var exe = GetServiceExe(info);
            if (exe == null)
                return null;
            var r = await RunExeVerb(exe, verb, timeout, log).ConfigureAwait(false);
            //  A time out isn't a sign of the wrong executable (the right one can hang), it's handled by the callers
            if (r.TimedOut || SmServiceCli.IsExpected(verb, r.Exited, r.ExitCode, r.Output))
                return r;
            var alt = await TryFallbackExe(info, exe, r).ConfigureAwait(false);
            if (alt == null)
                return r;
            return await RunExeVerb(alt, verb, timeout, log).ConfigureAwait(false);
        }

        /// <summary>
        /// Probe the other executables (best guess first, at most 3) with "status", switch to the first one that responds as a service.
        /// Not done for a user selected executable, and at most once per <see cref="ExeFallbackInterval"/>.
        /// </summary>
        /// <returns>The new executable, null if none was found</returns>
        async Task<String> TryFallbackExe(SmServiceInfo info, String current, ExternalProcessResult failed)
        {
            if (info.Exe?.Manual ?? false)
                return null;
            var now = DateTime.UtcNow.Ticks;
            var last = Interlocked.Read(ref info.LastExeFallback);
            if ((now - last) < ExeFallbackInterval.Ticks)
                return null;
            if (Interlocked.CompareExchange(ref info.LastExeFallback, now, last) != last)
                return null;
            var name = info.Service.Name;
            var m = Manager;
            m.AddMessage(MessageLevels.Warning, $"{LogPrefix}The executable {current.ToQuoted()} of service \"{name}\" didn't respond as a service ({failed}), trying the other executables");
            var others = GetCandidates(info).Where(x => !String.Equals(x.FullName, current, PathCmp)).Take(3).ToList();
            foreach (var c in others)
            {
                if (!c.CanExecute)
                    PlatformTools.Current.MakeExecutable(c.FullName);
                var p = await RunExeVerb(c.FullName, "status", StatusTimeout).ConfigureAwait(false);
                if (!SmServiceCli.IsServiceStatusResponse(p.Exited, p.ExitCode, p.Output))
                    continue;
                lock (info.ExeLock)
                {
                    //  A user may have selected an executable meanwhile
                    if (info.Exe?.Manual ?? false)
                        return null;
                    KeyValueStore.AllApp.Set(ExeKey(name), new SmExeSelection { Name = c.Name, Manual = false });
                    info.Exe = new SmExeState
                    {
                        FullName = c.FullName,
                        Manual = false,
                        Resolved = DateTime.UtcNow,
                    };
                }
                m.AddMessage(MessageLevels.Warning, $"{LogPrefix}Switched the executable of service \"{name}\" from \"{Path.GetFileName(current)}\" to \"{c.Name}\"");
                return c.FullName;
            }
            m.AddMessage(MessageLevels.Warning, $"{LogPrefix}None of the other executables of service \"{name}\" responds as a service, select the executable on the service page if the wrong one is used");
            return null;
        }

        /// <summary>
        /// Get the status of a managed service ("status" verb, silent).
        /// </summary>
        async Task<ServiceStatus> CheckServiceStatus(SmServiceInfo info)
        {
            using var _ = PerfMon.Track(nameof(CheckServiceStatus));
            try
            {
                var r = await RunServiceVerb(info, "status", StatusTimeout, false).ConfigureAwait(false);
                if (r == null)
                    return ServiceStatus.NotInstalled;
                return SmServiceCli.ParseStatus(r.Exited, r.ExitCode, r.Output);
            }
            catch (Exception ex)
            {
                StatusEx.OnException(ex);
                return ServiceStatus.Unknown;
            }
        }

        /// <summary>
        /// Kill every process running from a folder (except this process).
        /// A process matches if it's executable is inside the folder, or if any (fully qualified) command line argument is (ex: "dotnet /opt/svc/Svc.dll", "dotnet C:\Svc\Svc.dll").
        /// Linux: the systemd units of the matched processes are first stopped (without waiting), so that a "Restart=" setting doesn't restart the service while the folders are swapped
        /// (a stop requested by systemctl is never restarted, and a restart from a missing folder could hit the start limit and make the next start fail).
        /// The unit of this process is never stopped.
        /// </summary>
        /// <returns>The number of processes that was killed</returns>
        async Task<int> KillProcessesIn(String folder)
        {
            var dir = TrimFolder(folder) + Path.DirectorySeparatorChar;
            var self = Environment.ProcessId;
            var linux = OperatingSystem.IsLinux();
            var hits = new List<(Process, String)>();
            foreach (var p in Process.GetProcesses())
            {
                String what = null;
                try
                {
                    if (p.Id != self)
                    {
                        what = SafeRead(() => p.MainModule?.FileName);
                        if ((what == null) || !what.StartsWith(dir, PathCmp))
                            what = SmProcessMatch.GetArgInFolder(SmProcessMatch.GetCommandLine(p.Id), dir, PathCmp == StringComparison.OrdinalIgnoreCase);
                    }
                }
                catch
                {
                    what = null;
                }
                if (what == null)
                    p.Dispose();
                else
                    hits.Add((p, what));
            }
            if (hits.Count == 0)
                return 0;
            if (linux)
            {
                var selfUnit = SmProcessMatch.GetServiceUnit(SmProcessMatch.ReadProc(self, "cgroup"));
                var units = hits
                    .Select(x => SmProcessMatch.GetServiceUnit(SmProcessMatch.ReadProc(x.Item1.Id, "cgroup")))
                    .Where(x => (x != null) && !String.Equals(x, selfUnit, StringComparison.Ordinal))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                foreach (var unit in units)
                {
                    Manager.AddMessage(MessageLevels.Warning, $"{LogPrefix}Stopping the systemd unit \"{unit}\" (so that it isn't restarted)");
                    var r = await ExternalProcess.RunWithTimeoutAsync("systemctl", ["stop", "--no-block", "--", unit], TimeSpan.FromSeconds(10)).ConfigureAwait(false);
                    if ((!r.Exited) || (r.ExitCode != 0))
                        Manager.AddMessage(MessageLevels.Warning, $"{LogPrefix}Failed to stop the systemd unit \"{unit}\": {(r.Exception?.Message ?? (r.TimedOut ? "timed out" : ("exit code " + r.ExitCode)))} {r.Output?.Trim()}");
                }
            }
            int n = 0;
            foreach (var (p, what) in hits)
            {
                using (p)
                {
                    try
                    {
                        Manager.AddMessage(MessageLevels.Warning, $"{LogPrefix}Killing process #{p.Id} {what.ToQuoted()}");
                        p.Kill(true);
                        ++n;
                    }
                    catch (Exception ex)
                    {
                        Manager.AddMessage(LogPrefix + "Failed to kill process #" + p.Id, ex, MessageLevels.Warning);
                    }
                }
            }
            return n;
        }

        /// <summary>
        /// Linux: add the execute permission to every executable in a folder that lacks it (by content: ELF executables and "#!" scripts).
        /// Uploaded files never have it (the upload format has no file mode, files are created with 0666 &amp; ~umask).
        /// </summary>
        void EnsureExecutables(String folder)
        {
            if (OperatingSystem.IsWindows())
                return;
            var pt = PlatformTools.Current;
            foreach (var x in pt.FindExecutables(folder))
            {
                if (x.CanExecute)
                    continue;
                var ex = pt.MakeExecutable(x.FullName);
                if (ex == null)
                    Manager.AddMessage(MessageLevels.Info, $"{LogPrefix}Added the execute permission to {x.FullName.ToQuoted()} ({x.Format})");
                else
                    Manager.AddMessage(LogPrefix + "Failed to add the execute permission to " + x.FullName.ToQuoted(), ex, MessageLevels.Warning);
            }
        }

        /// <summary>
        /// The executable details of a service (for <see cref="SmServiceDetail"/>).
        /// </summary>
        SmExecutable[] GetExecutables(SmServiceInfo info, String exe)
        {
            var current = exe == null ? null : Path.GetFileName(exe);
            return GetCandidates(info).Select(x => new SmExecutable(x, current)).ToArray();
        }

        /// <summary>
        /// Select the executable of a managed service.
        /// The executable is used for all service operations (status, start, stop ...) as long as it exists, it's never replaced automatically.
        /// </summary>
        /// <param name="r">The service name and the executable (one of <see cref="SmServiceDetail.Executables"/>), an empty name selects automatically (the best guess, replaced if it doesn't respond as a service)</param>
        /// <param name="context"></param>
        /// <returns>Updated service details</returns>
        [WebApi]
        [WebApiAuth]
        [WebApiAudit(AuditGroup)]
        public async Task<SmServiceDetail> SelectExe(SmSelectExeRequest r, HttpServerRequest context)
        {
            if (r == null)
                throw new HttpResponseException(400, "No request");
            var info = Validate(r.ServiceName, context);
            var name = info.Service.Name;
            var exeName = r.ExeName?.Trim();
            var key = ExeKey(name);
            if (String.IsNullOrEmpty(exeName))
            {
                lock (info.ExeLock)
                {
                    KeyValueStore.AllApp.Delete(key);
                    InvalidateExe(info);
                    ResolveExe(info);
                }
                Manager.AddMessage(MessageLevels.Info, $"{LogPrefix}The executable of service \"{name}\" is selected automatically");
            }
            else
            {
                if (!IsPlainFileName(exeName))
                    throw new HttpResponseException(400, "Invalid executable name");
                info.Candidates = null;
                var c = GetCandidates(info).FirstOrDefault(x => String.Equals(x.Name, exeName, PathCmp));
                if (c == null)
                    throw new HttpResponseException(409, "\"" + exeName + "\" isn't an executable in the active folder (refresh the page)");
                if (!c.CanExecute)
                    PlatformTools.Current.MakeExecutable(c.FullName);
                lock (info.ExeLock)
                {
                    KeyValueStore.AllApp.Set(key, new SmExeSelection { Name = c.Name, Manual = true });
                    info.Exe = new SmExeState
                    {
                        FullName = c.FullName,
                        Manual = true,
                        Resolved = DateTime.UtcNow,
                    };
                }
                Manager.AddMessage(MessageLevels.Info, $"{LogPrefix}Selected \"{c.Name}\" as the executable of service \"{name}\"");
            }
            Interlocked.Exchange(ref info.Status, await CheckServiceStatus(info).ConfigureAwait(false));
            //  Every session: the cached tables of the other users' sessions are stale too (AS7-12)
            context.Server.InvalidateCacheEverywhere();
            return InternalGetDetail(info);
        }

        #region Self update

        /// <summary>
        /// The <see cref="FsManagedFolder.OnSwapAsync"/> of the managed services: when the active folder is the folder that this process runs from (a self update),
        /// the swap is done by a detached script (see <see cref="SelfFolderSwap"/>), else null is returned (the normal in process swap).
        /// </summary>
        Task<Exception> OnServiceSwap(String name, String targetFolder, String backupFolder, String newFolder)
        {
            if (!SelfFolderSwap.IsRunningFrom(targetFolder))
                return null;
            try
            {
                if (!Services.TryGetValue(name.FastToLower(), out var info))
                    return Task.FromResult(new Exception("Unknown service \"" + name + "\""));
                var oldExe = GetServiceExe(info);
                if (oldExe == null)
                    return Task.FromResult(new Exception("Can't find the executable of service \"" + name + "\" in \"" + targetFolder + "\""));
                EnsureExecutables(newFolder);
                var newExe = FindExeIn(info, newFolder);
                if (newExe == null)
                    return Task.FromResult(new Exception("Can't find an executable in \"" + newFolder + "\""));
                //  Never replace an existing backup (the folder sync swap would), use the next free "_N" suffix (a valid version name)
                var bak = backupFolder;
                for (int i = 2; Directory.Exists(bak) || File.Exists(bak); ++i)
                    bak = backupFolder + "_" + i;
                var p = P;
                var o = new SelfFolderSwap.Options
                {
                    UninstallTimeoutSeconds = Math.Max(1, p.SwapStopTimeoutSeconds),
                    StableSeconds = Math.Max(4, p.SelfUpdateStableSeconds),
                    HealthUrl = String.IsNullOrWhiteSpace(p.SelfUpdateHealthUrl) ? null : p.SelfUpdateHealthUrl.Trim(),
                };
                var ex = SelfFolderSwap.TryLaunch(targetFolder, bak, newFolder, oldExe, newExe, o, out var script, out var log);
                if (ex == null)
                    Manager.AddMessage(MessageLevels.Warning, $"{LogPrefix}Self update of \"{name}\": started {script.ToQuoted()}, this process will be stopped and \"{Path.GetFileName(newFolder)}\" activated (log: {log.ToQuoted()})");
                return Task.FromResult(ex);
            }
            catch (Exception ex)
            {
                return Task.FromResult(ex);
            }
        }

        /// <summary>
        /// Report the outcome of the self update scripts that finished recently (they ran while this process was stopped / started).
        /// </summary>
        void ReportSelfUpdates()
        {
            try
            {
                var started = Process.GetCurrentProcess().StartTime.ToUniversalTime();
                var dir = new DirectoryInfo(Path.GetTempPath());
                foreach (var f in dir.EnumerateFiles(SelfFolderSwap.LogFilePattern))
                {
                    //  Only the scripts that ran around the start of this process
                    if (f.LastWriteTimeUtc < started.AddMinutes(-10))
                        continue;
                    var text = File.ReadAllText(f.FullName);
                    var res = SelfFolderSwap.GetResult(text);
                    var msg = res switch
                    {
                        0 => "the new version is running",
                        1 => "aborted before any change",
                        2 => "FAILED, the previous version was restored",
                        3 => "FAILED, the previous version is running from the backup folder, manual clean up required",
                        4 => "FAILED, manual intervention required",
                        _ => "still running or interrupted",
                    };
                    Manager.AddMessage(res == 0 ? MessageLevels.Info : MessageLevels.Warning, $"{LogPrefix}Self update {f.Name}: {msg} (log: {f.FullName.ToQuoted()})");
                }
            }
            catch (Exception ex)
            {
                Manager.AddMessage(LogPrefix + "Failed to read the self update logs", ex, MessageLevels.Debug);
            }
        }

        #endregion//Self update
    }
}
