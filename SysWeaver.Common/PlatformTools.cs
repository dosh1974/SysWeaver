using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SysWeaver
{
    public static class PlatformTools
    {
        static readonly IPlatformTools Dummy = new DummyPlatformTools("Unknown");

        static IPlatformTools Get(String p)
        {
            if (p == null)
                return Dummy;
            var name = p.MakeFirstUppercase();
            var tools = Tools;
            if (tools.TryGetValue(name, out var tool))
                return tool;
            lock (tools)
            {
                if (tools.TryGetValue(name, out tool))
                    return tool;
                var t = typeof(PlatformTools);
                var asmName = String.Join('.', t.Assembly.GetName().Name, name);
                var className = String.Concat(t.Namespace, ".", name, "PlatformTools");
                var typeName = String.Join(", ", className, asmName);
                Type type = null;
                try
                {
                    type = TypeFinder.Get(typeName);
                    if (type == null)
                    {
                        tool = new DummyPlatformTools(name + " [Type not found]");
                        tools.TryAdd(name, tool);
                        return tool;
                    }
                    tool = Activator.CreateInstance(type) as IPlatformTools;
                    tools.TryAdd(name, tool);
                    return tool;
                }
                catch (Exception ex)
                {
                    if (type == null)
                    {
                        tool = new DummyPlatformTools(String.Concat(name, " [Type failed: ", ex.Message, ']'));
                        tools.TryAdd(name, tool);
                        return tool;
                    }
                    tool = new DummyPlatformTools(String.Concat(name, " [New failed: ", ex.Message, ']'));
                    tools.TryAdd(name, tool);
                    return tool;
                }
            }
        }

        static readonly ConcurrentDictionary<String, IPlatformTools> Tools = new ConcurrentDictionary<string, IPlatformTools>(StringComparer.Ordinal);

        /// <summary>
        /// The platform tools for the OS that the current process is running under.
        /// </summary>
        public static readonly IPlatformTools Current = Get(EnvInfo.OsPlatform);

    }


    public interface IPlatformTools : IHaveStats
    {
        /// <summary>
        /// Name of the platform
        /// </summary>
        String Name { get; }

        /// <summary>
        /// The friendly name of the OS
        /// </summary>
        String OsFriendlyName { get; }


        /// <summary>
        /// The default folder for key files
        /// </summary>
        String DefaultKeyDir { get; }

        /// <summary>
        /// Flush (write through) a file to disc
        /// </summary>
        /// <param name="h">The handle to the file</param>
        /// <returns>True if successful (and supported)</returns>
        bool FlushToDisc(SafeHandle h);

        /// <summary>
        /// Get system memory information (physical)
        /// </summary>
        /// <param name="availableBytes">Current number of free bytes</param>
        /// <param name="totalBytes">Installed (available) memory bytes</param>
        /// <returns>True if successful (and supported)</returns>
        bool GetMemorySize(out ulong availableBytes, out ulong totalBytes);



        /// <summary>
        /// Get the current CPU usage as a percentage
        /// </summary>
        /// <param name="cpuUsage">[0, 100] the cpu usage as a percentage</param>
        /// <returns>True if successful (and supported)</returns>
        bool GetCpuUsage(out double cpuUsage);

        /// <summary>
        /// Reboot the computer
        /// </summary>
        /// <returns></returns>
        bool Reboot();

        /// <summary>
        /// Make a directory accesible to all users
        /// </summary>
        /// <param name="directoryName">The full patch to the directory</param>
        /// <returns>null is succsessful</returns>
        Exception MakeDirectoryAccessableToEveryOne(String directoryName);

    }

    public sealed class DummyPlatformTools : IPlatformTools
    {
        public DummyPlatformTools(String name)
        {
            Name = name;
        }

        public string Name { get; init; }

        public string OsFriendlyName { get; } = Environment.OSVersion.ToString();

        public String DefaultKeyDir => @"C:\Keys";

        public bool FlushToDisc(SafeHandle h) => true;
        public bool GetMemorySize(out ulong availableBytes, out ulong totalBytes)
        {
            availableBytes = 0;
            totalBytes = 0;
            return false;
        }
        public bool GetCpuUsage(out double cpuUsage)
        {
            cpuUsage = 0;
            return false;
        }

        public bool Reboot()
            => false;

        public Exception MakeDirectoryAccessableToEveryOne(String directoryName) => null;

        #region IHaveStats

        public IEnumerable<Stats> GetStats() => Enumerable.Empty<Stats>();

        #endregion//IHaveStats

    }


}
