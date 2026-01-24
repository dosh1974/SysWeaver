using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace SysWeaver
{
    public sealed class LinuxPlatformTools : IPlatformTools
    {
        public string Name => "Linux";

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
                        var lines = File.ReadAllLines(x);
                        foreach (var line in lines)
                        {
                            var tl = line.Trim();
                            if (tl.Length <= 0)
                                continue;
                            if (tl[0] == '#')
                                continue;
                            tl = tl.SplitFirst('#');
                            tl = tl.SplitFirst('=', out var value);
                            if (value == null)
                                continue;
                            tl = tl.TrimEnd();
                            value = value.Trim().RemoveQuotes();
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


        public String DefaultKeyDir => @"/etc/keys";

        public bool FlushToDisc(SafeHandle h)
        {
            //  TODO: What?
            return true;
        }

        static readonly object _linuxMemoryLock = new();
        static readonly char[] _arrayForMemInfoRead = new char[200];

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

        public Exception MakeDirectoryAccessableToEveryOne(String directoryName) => null;

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

        public IEnumerable<Stats> GetStats()
            => Exs.GetStats("Linux.Platform", "Exception.");

        #endregion//IHaveStats


    }


}
