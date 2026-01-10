using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace SysWeaver
{
    public sealed class LinuxPlatformTools : IPlatformTools
    {
        public string Name => "Linux";

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
            catch
            {
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
                cpuUsage = 0;
                return false;
            }
            catch
            {
                cpuUsage = 0;
                return false;
            }
        }


    }


}
