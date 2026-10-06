using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SysWeaver
{
    /// <summary>
    /// Provides access to the platform (OS) specific tools implementation (<see cref="IPlatformTools"/>).
    /// </summary>
    /// <remarks>
    /// The implementation is loaded at runtime by convention: for the platform name "Windows" the type "SysWeaver.WindowsPlatformTools"
    /// in the assembly "SysWeaver.Common.Windows" is instantiated (using <see cref="TypeFinder"/>).
    /// If it can't be found or created, a <see cref="DummyPlatformTools"/> is used instead (its name describes the failure).
    /// </remarks>
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
        /// The platform tools for the OS that the current process is running under (based on <see cref="EnvInfo.OsPlatform"/>).
        /// Never null in practice, falls back to a <see cref="DummyPlatformTools"/>.
        /// </summary>
        public static readonly IPlatformTools Current = Get(EnvInfo.OsPlatform);

    }


    /// <summary>
    /// Platform (OS) specific functionality, implemented in separate platform assemblies and accessed through <see cref="PlatformTools.Current"/>.
    /// </summary>
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
        /// <returns>True if the reboot was initiated (and supported)</returns>
        bool Reboot();

        /// <summary>
        /// Make a directory accessible to all users
        /// </summary>
        /// <param name="directoryName">The full path to the directory</param>
        /// <returns>null if successful, else the exception that occurred</returns>
        Exception MakeDirectoryAccessableToEveryOne(String directoryName);

    }

    /// <summary>
    /// Fallback <see cref="IPlatformTools"/> used when no platform specific implementation is available, most operations are no-ops that report "not supported".
    /// </summary>
    public sealed class DummyPlatformTools : IPlatformTools
    {
        /// <summary>
        /// Create a dummy platform tools instance.
        /// </summary>
        /// <param name="name">The name to report, typically the platform name and the reason why the real implementation couldn't be used.</param>
        public DummyPlatformTools(String name)
        {
            Name = name;
        }

        /// <inheritdoc/>
        public string Name { get; init; }

        /// <summary>
        /// The OS description from <see cref="Environment.OSVersion"/>.
        /// </summary>
        public string OsFriendlyName { get; } = Environment.OSVersion.ToString();

        /// <summary>
        /// Always "C:\Keys" (regardless of OS).
        /// </summary>
        public String DefaultKeyDir => @"C:\Keys";

        /// <summary>
        /// Does nothing, but returns true (unlike the other unsupported operations).
        /// </summary>
        /// <param name="h">Ignored.</param>
        /// <returns>Always true.</returns>
        public bool FlushToDisc(SafeHandle h) => true;

        /// <summary>
        /// Not supported.
        /// </summary>
        /// <param name="availableBytes">Always 0.</param>
        /// <param name="totalBytes">Always 0.</param>
        /// <returns>Always false.</returns>
        public bool GetMemorySize(out ulong availableBytes, out ulong totalBytes)
        {
            availableBytes = 0;
            totalBytes = 0;
            return false;
        }
        /// <summary>
        /// Not supported.
        /// </summary>
        /// <param name="cpuUsage">Always 0.</param>
        /// <returns>Always false.</returns>
        public bool GetCpuUsage(out double cpuUsage)
        {
            cpuUsage = 0;
            return false;
        }

        /// <summary>
        /// Not supported.
        /// </summary>
        /// <returns>Always false.</returns>
        public bool Reboot()
            => false;

        /// <summary>
        /// Does nothing.
        /// </summary>
        /// <param name="directoryName">Ignored.</param>
        /// <returns>Always null (reported as success).</returns>
        public Exception MakeDirectoryAccessableToEveryOne(String directoryName) => null;

        #region IHaveStats

        /// <summary>
        /// No statistics are available.
        /// </summary>
        /// <returns>An empty sequence.</returns>
        public IEnumerable<Stats> GetStats() => Enumerable.Empty<Stats>();

        #endregion//IHaveStats

    }


}
