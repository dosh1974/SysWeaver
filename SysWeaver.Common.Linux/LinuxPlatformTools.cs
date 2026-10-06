using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace SysWeaver
{
    /// <summary>
    /// Linux implementation of <see cref="IPlatformTools"/>.
    /// </summary>
    /// <remarks>
    /// <para>Not referenced directly: <see cref="PlatformTools.Current"/> loads it by name ("SysWeaver.LinuxPlatformTools, SysWeaver.Common.Linux") when running on Linux,
    /// so the assembly only needs to be deployed with the application.</para>
    /// <para>Uses /etc/*-release for the OS name, /proc/meminfo for memory and the output of "top -b -n 1" for CPU usage.
    /// <see cref="FlushToDisc(SafeHandle)"/> and <see cref="MakeDirectoryAccessableToEveryOne(string)"/> are no-ops.</para>
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
        /// <remarks>Thread safe (serialized by a static lock, since a static buffer is reused). Only the first 200 characters of the file are read.</remarks>
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
        /// Not implemented on Linux: does nothing.
        /// </summary>
        /// <param name="directoryName">The directory (ignored).</param>
        /// <returns>Always null (reported as success).</returns>
        public Exception MakeDirectoryAccessableToEveryOne(String directoryName) => null;

        /// <summary>
        /// Gets the total CPU usage by running "top -b -n 1" through /bin/bash and parsing the idle value of the "%Cpu(s):" line.
        /// </summary>
        /// <param name="cpuUsage">Receives the CPU usage in percent [0, 100], 0 on failure.</param>
        /// <returns>True if successful, false on failure (the exception is tracked in the stats).</returns>
        /// <remarks>Starts a process on every call, so it is relatively expensive. The number is parsed using the current culture.</remarks>
        public bool GetCpuUsage(out double cpuUsage)
        {
            try
            {
                var pi = new ProcessStartInfo();
                pi.FileName = "/bin/bash";
                pi.Arguments = "-c \"top -b -n 1\"";
                pi.RedirectStandardOutput = true;
                String output;
                using (var process = Process.Start(pi))
                    output = process.StandardOutput.ReadToEnd();
                foreach (var x in output.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                {
                    if (x.FastStartsWith("%Cpu(s):"))
                    {
                        var times = x.Substring(8).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        var idleS = times[3];
                        var idle = double.Parse(idleS.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0]);
                        var c = 100.0 - idle;
                        if (c < 0)
                            c = 0;
                        if (c > 100)
                            c = 100;
                        cpuUsage = c;
                        return true;
                    }
                }
                throw new Exception("No \"%Cpu(s)\" row found in the output from \"top -b -n 1\"");
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
