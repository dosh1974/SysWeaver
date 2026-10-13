using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace SysWeaver
{
    /// <summary>
    /// Linux implementation of <see cref="IPlatformTools"/>.
    /// </summary>
    /// <remarks>
    /// <para>Not referenced directly: <see cref="PlatformTools.Current"/> loads it by name ("SysWeaver.LinuxPlatformTools, SysWeaver.Common.Linux") when running on Linux,
    /// so the assembly only needs to be deployed with the application.</para>
    /// <para>Uses /etc/*-release for the OS name, /proc/meminfo for memory and two samples of /proc/stat for CPU usage.
    /// <see cref="FlushToDisc(SafeHandle)"/> is a no-op. <see cref="MakeDirectoryAccessableToEveryOne(string)"/> uses mode bits and POSIX default ACL's (setfacl).</para>
    /// <para>Failures are counted in an <see cref="ExceptionTracker"/> exposed through <see cref="GetStats"/>.</para>
    /// </remarks>
    public sealed class LinuxPlatformTools : IPlatformTools
    {
        /// <summary>
        /// Returns "Linux".
        /// </summary>
        public string Name => "Linux";

        /// <summary>
        /// The PRETTY_NAME value from the /etc/*-release files (ex: "Ubuntu 24.04 LTS"), or <see cref="Environment.OSVersion"/> if not found.
        /// </summary>
        public string OsFriendlyName { get; } = Get("PRETTY_NAME") ?? Environment.OSVersion.ToString();

        static String Get(String key) => OsData.TryGetValue(key.FastToLower(), out var v) ? v : Environment.OSVersion.ToString();

        static IReadOnlyDictionary<String, String> GetOsData()
        {
            Dictionary<String, String> d = new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                foreach (var x in Directory.GetFiles("/etc", "*-release"))
                {
                    try
                    {
                        var lines = FileExt.ReadLines(x, null, true, true);
                        foreach (var line in lines)
                        {
                            if (line[0] == '#')
                                continue;
                            var tl = line.SplitFirst('#', true);
                            tl = tl.SplitFirst('=', out var value, false);
                            if (value == null)
                                continue;
                            value = value.RemoveQuotes();
                            d[tl.FastToLower()] = value;
                        }
                    }
                    catch
                    {

                    }
                }
            }
            catch
            {
            }
            return d.Freeze();
        }


        static readonly IReadOnlyDictionary<String, String> OsData = GetOsData();


        /// <summary>
        /// Returns "/etc/keys".
        /// </summary>
        public String DefaultKeyDir => @"/etc/keys";

        /// <summary>
        /// Not implemented on Linux: does nothing (no fsync is performed) and always returns true.
        /// </summary>
        /// <param name="h">The file handle (ignored).</param>
        /// <returns>Always true.</returns>
        public bool FlushToDisc(SafeHandle h)
        {
            //  TODO: What?
            return true;
        }

        static readonly object _linuxMemoryLock = new();
        static readonly char[] _arrayForMemInfoRead = new char[200];

        /// <summary>
        /// Gets the physical memory size from the MemTotal and MemAvailable entries of /proc/meminfo.
        /// </summary>
        /// <param name="availableBytes">Receives the available memory in bytes (MemAvailable), 0 on failure.</param>
        /// <param name="totalBytes">Receives the total memory in bytes (MemTotal), 0 on failure.</param>
        /// <returns>True if successful, false on failure (the exception is tracked in the stats).</returns>
        /// <remarks>Thread safe (serialized by a static lock, since a static buffer is reused). Only the first 200 characters of the file are read,
        /// if an entry isn't found there (ex: kernels older than 3.14 have no MemAvailable) false is returned.</remarks>
        public bool GetMemorySize(out ulong availableBytes, out ulong totalBytes)
        {
            try
            {
                lock (_linuxMemoryLock) // lock because of reusing static fields due to optimization
                {
                    totalBytes = GetBytesCountFromLinuxMemInfo("MemTotal:", true);
                    availableBytes = GetBytesCountFromLinuxMemInfo("MemAvailable:", false);
                }
                return true;
            }
            catch (Exception ex)
            {
                Exs.OnException(ex);
                availableBytes = 0;
                totalBytes = 0;
                return false;
            }
        }

        static ulong GetBytesCountFromLinuxMemInfo(string token, bool refreshFromFile)
        {
            // NOTE: Using the linux file /proc/meminfo which is refreshed frequently and starts with:
            //MemTotal:        7837208 kB
            //MemFree:          190612 kB
            //MemAvailable:    5657580 kB
            var readSpan = _arrayForMemInfoRead.AsSpan();
            if (refreshFromFile)
            {
                using var fileStream = new FileStream("/proc/meminfo", FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(fileStream, Encoding.UTF8, leaveOpen: true);
                reader.ReadBlock(readSpan);
            }
            var tokenIndex = readSpan.IndexOf(token);
            if (tokenIndex < 0)
                throw new InvalidDataException(String.Concat("No \"", token, "\" entry found at the start of /proc/meminfo"));
            var fromTokenSpan = readSpan.Slice(tokenIndex + token.Length);
            var kbIndex = fromTokenSpan.IndexOf("kB");
            var notTrimmedSpan = fromTokenSpan.Slice(0, kbIndex);
            var trimmedSpan = notTrimmedSpan.Trim(' ');
            var kBytesCount = ulong.Parse(trimmedSpan);
            var bytesCount = kBytesCount * 1024;
            return bytesCount;
        }

        /// <summary>
        /// Starts "/usr/bin/sudo /sbin/reboot" (requires that the process may run it via sudo without a password prompt).
        /// </summary>
        /// <returns>True if the process could be started (not that the reboot succeeded), false if starting it failed.</returns>
        public bool Reboot()
        {
            var pi = new ProcessStartInfo();
            pi.FileName = "/usr/bin/sudo";
            pi.Arguments = "/sbin/reboot";
            try
            {
                using var _ = Process.Start(pi);
            }
            catch
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Make a directory, everything in it and everything created in it later, readable and writable by all users
        /// (mimics the Windows implementation that grants "Everyone" full control, inherited by all files and sub directories).
        /// This makes files and folders created by a service (ex: running as root) modifiable by users and vice versa.
        /// </summary>
        /// <param name="directoryName">The full path to the directory.</param>
        /// <returns>null if successful, else the exception that occurred (also tracked in the stats).</returns>
        /// <remarks>
        /// <para>The directory and all existing files and sub directories (recursively) get read and write access for owner, group and others (like "chmod -R a+rwX"):
        /// directories get rwx for all and the setgid bit (so new entries inherit the group), files keep their execute bits
        /// (if any execute bit is set, it's set for all).
        /// Entries that already have the required bits aren't touched. Symbolic links are skipped (never followed).
        /// The sticky bit is never set, so users can modify and delete each others files.</para>
        /// <para>Files and directories created later (by any user, regardless of their umask) are made modifiable by everyone using POSIX default ACL's:
        /// "setfacl -R -P -d -m u::rwx,g::rwx,o::rwx,m::rwx" is run on the directory (new files get rw for all, new directories rwx for all and the same default ACL).
        /// If setfacl isn't installed (package "acl") or fails (ex: the file system doesn't support ACL's), the mode bits (and setgid) are still applied,
        /// but an exception describing the problem is returned (files created later then get the permissions given by the creator's umask).</para>
        /// <para>Only the owner of an entry (or root) may change its permissions, entries that can't be changed are reported in the returned exception (the rest is still processed).</para>
        /// <para>A directory that was successfully set up (including the default ACL) is remembered for the lifetime of the process,
        /// calling it again for that directory or any directory inside it returns null without doing anything.</para>
        /// <para>Granting everyone write access makes the folder writable by any local user, only use it for data that isn't security sensitive.</para>
        /// </remarks>
        public Exception MakeDirectoryAccessableToEveryOne(String directoryName)
        {
            try
            {
                var fullName = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directoryName));
                //  Already done (default ACL's are inherited by new sub directories)?
                var done = DoneDirs;
                for (var p = fullName; p != null; p = Path.GetDirectoryName(p))
                    if (done.ContainsKey(p))
                        return null;
                var di = new DirectoryInfo(fullName);
                if (!di.Exists)
                    throw new DirectoryNotFoundException("The directory \"" + fullName + "\" doesn't exist!");
                if (di.LinkTarget != null)
                    throw new IOException("The directory \"" + fullName + "\" is a symbolic link, refusing to change permissions!");
                List<Exception> errors = null;
                SetAllAccess(di, true, ref errors);
                foreach (var x in di.EnumerateFileSystemInfos("*", AllEntries))
                {
                    if ((x.Attributes & FileAttributes.ReparsePoint) != 0)
                        continue;
                    SetAllAccess(x, x is DirectoryInfo, ref errors);
                }
                var aclError = SetDefaultAcl(fullName);
                if (aclError != null)
                    (errors ??= new List<Exception>()).Add(aclError);
                if (errors == null)
                {
                    done.TryAdd(fullName, 0);
                    return null;
                }
                var ex = errors.Count == 1 ? errors[0] : new AggregateException("Failed to make \"" + fullName + "\" accessible to everyone", errors);
                Exs.OnException(ex);
                return ex;
            }
            catch (Exception ex)
            {
                Exs.OnException(ex);
                return ex;
            }
        }

        static readonly LowAllocConcurrentDictionary<String, int> DoneDirs = new(StringComparer.Ordinal);

        static readonly EnumerationOptions AllEntries = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            ReturnSpecialDirectories = false,
            MatchType = MatchType.Simple,
        };

        const UnixFileMode AllRw = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.OtherRead | UnixFileMode.OtherWrite;
        const UnixFileMode AllX = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

#pragma warning disable CA1416 // Only used on Linux
        static void SetAllAccess(FileSystemInfo x, bool isDir, ref List<Exception> errors)
        {
            try
            {
                var m = x.UnixFileMode;
                var n = m | AllRw;
                if (isDir)
                    n |= AllX | UnixFileMode.SetGroup;
                else if ((m & AllX) != 0)
                    n |= AllX;
                if (n == m)
                    return;
                //  Never follow symbolic links (could point anywhere)
                if (x.LinkTarget != null)
                    return;
                x.UnixFileMode = n;
            }
            catch (Exception ex)
            {
                (errors ??= new List<Exception>()).Add(ex);
            }
        }
#pragma warning restore CA1416

        static readonly String SetFaclPath = new[] { "/usr/bin/setfacl", "/bin/setfacl", "/usr/local/bin/setfacl", "/usr/sbin/setfacl", "/sbin/setfacl" }.FirstOrDefault(File.Exists);

        static Exception SetDefaultAcl(String dir)
        {
            var exe = SetFaclPath;
            if (exe == null)
                return new PlatformNotSupportedException("The \"setfacl\" tool (package \"acl\") isn't installed, the permissions of \"" + dir + "\" and it's content was updated, but files and folders created later in it will get the permissions given by the creator's umask!");
            try
            {
                var pi = new ProcessStartInfo(exe);
                //  -R: recursive, -P: don't follow symbolic links, -d: default ACL (only applies to directories)
                pi.ArgumentList.Add("-R");
                pi.ArgumentList.Add("-P");
                pi.ArgumentList.Add("-d");
                pi.ArgumentList.Add("-m");
                pi.ArgumentList.Add("u::rwx,g::rwx,o::rwx,m::rwx");
                pi.ArgumentList.Add("--");
                pi.ArgumentList.Add(dir);
                pi.UseShellExecute = false;
                pi.RedirectStandardOutput = true;
                pi.RedirectStandardError = true;
                pi.Environment["LC_ALL"] = "C";
                using var process = Process.Start(pi);
                var errTask = process.StandardError.ReadToEndAsync();
                process.StandardOutput.ReadToEnd();
                var err = errTask.GetAwaiter().GetResult();
                process.WaitForExit();
                if (process.ExitCode == 0)
                    return null;
                return new IOException(String.Concat("Failed to set the default ACL of \"", dir, "\" (exit code ", process.ExitCode.ToString(CultureInfo.InvariantCulture), "), files and folders created later in it may get the permissions given by the creator's umask: ", err?.Trim()));
            }
            catch (Exception ex)
            {
                return new IOException("Failed to run \"" + exe + "\" to set the default ACL of \"" + dir + "\", files and folders created later in it will get the permissions given by the creator's umask: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// The time between the two /proc/stat samples of <see cref="GetCpuUsage(out double)"/> in milliseconds.
        /// </summary>
        const int CpuSampleIntervalMs = 250;

        /// <summary>
        /// Parses the aggregated "cpu" line of /proc/stat ("cpu  user nice system idle iowait irq softirq steal guest guest_nice", in clock ticks since boot).
        /// </summary>
        /// <param name="procStat">The text of /proc/stat (only the line starting with "cpu " is used).</param>
        /// <param name="idle">Receives the idle time (idle + iowait).</param>
        /// <param name="total">Receives the total time (user + nice + system + idle + iowait + irq + softirq + steal; guest time is already included in user / nice). Missing trailing values (old kernels) count as 0.</param>
        /// <returns>True if the line was found and parsed, false otherwise.</returns>
        internal static bool TryParseProcStatCpu(String procStat, out ulong idle, out ulong total)
        {
            idle = 0;
            total = 0;
            if (procStat == null)
                return false;
            foreach (var line in procStat.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (!line.FastStartsWith("cpu "))
                    continue;
                var values = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                //  values[0] is "cpu", at least user, nice, system and idle are needed
                var vl = values.Length;
                if (vl < 5)
                    return false;
                if (vl > 9)
                    vl = 9;
                ulong t = 0;
                for (int i = 1; i < vl; ++i)
                {
                    if (!ulong.TryParse(values[i], NumberStyles.None, CultureInfo.InvariantCulture, out var v))
                        return false;
                    t += v;
                    if ((i == 4) || (i == 5))
                        idle += v;
                }
                total = t;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Computes the CPU usage in percent [0, 100] between two samples of /proc/stat (see <see cref="TryParseProcStatCpu"/>).
        /// </summary>
        /// <param name="idle0">The idle time of the first sample.</param>
        /// <param name="total0">The total time of the first sample.</param>
        /// <param name="idle1">The idle time of the second sample.</param>
        /// <param name="total1">The total time of the second sample.</param>
        /// <returns>The CPU usage in percent, 0 if no time passed (or the counters went backwards).</returns>
        internal static double ComputeCpuUsage(ulong idle0, ulong total0, ulong idle1, ulong total1)
        {
            if ((total1 <= total0) || (idle1 < idle0))
                return 0;
            var dt = (double)(total1 - total0);
            var c = 100.0 * (1.0 - (idle1 - idle0) / dt);
            if (c < 0)
                c = 0;
            if (c > 100)
                c = 100;
            return c;
        }

        static void SampleProcStatCpu(out ulong idle, out ulong total)
        {
            if (!TryParseProcStatCpu(File.ReadAllText("/proc/stat"), out idle, out total))
                throw new InvalidDataException("No valid \"cpu\" line found in /proc/stat");
        }

        /// <summary>
        /// Gets the current total CPU usage by sampling the aggregated "cpu" line of /proc/stat twice, 250 ms apart.
        /// </summary>
        /// <param name="cpuUsage">Receives the CPU usage in percent [0, 100] during the sampling interval, 0 on failure.</param>
        /// <returns>True if successful, false on failure (the exception is tracked in the stats).</returns>
        /// <remarks>Blocks the calling thread for the sampling interval (250 ms). Idle time includes iowait.</remarks>
        public bool GetCpuUsage(out double cpuUsage)
        {
            try
            {
                SampleProcStatCpu(out var idle0, out var total0);
                Thread.Sleep(CpuSampleIntervalMs);
                SampleProcStatCpu(out var idle1, out var total1);
                cpuUsage = ComputeCpuUsage(idle0, total0, idle1, total1);
                return true;
            }
            catch (Exception ex)
            {
                Exs.OnException(ex);
                cpuUsage = 0;
                return false;
            }
        }

        #region IHaveStats

        readonly ExceptionTracker Exs = new ExceptionTracker();

        /// <summary>
        /// Returns the exception statistics (system "Linux.Platform", names prefixed with "Exception.").
        /// </summary>
        /// <returns>The statistics.</returns>
        public IEnumerable<Stats> GetStats()
            => Exs.GetStats("Linux.Platform", "Exception.");

        #endregion//IHaveStats


    }


}
