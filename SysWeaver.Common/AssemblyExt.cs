using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace SysWeaver
{
    public static class AssemblyExt
    {
        /// <summary>
        /// Get all embedded resourves in the assembly (this cached)
        /// </summary>
        /// <param name="assembly"></param>
        /// <returns></returns>
        public static IReadOnlySet<String> GetEmbeddedResource(this Assembly assembly)
        {
            var c = EmbeddedResources;
            if (c.TryGetValue(assembly, out var r))
                return r;
            r = new HashSet<String>(assembly.GetManifestResourceNames(), StringComparer.Ordinal).Freeze();
            return c.TryAdd(assembly, r) ? r : c[assembly];
        }

        static readonly ConcurrentDictionary<Assembly, IReadOnlySet<String>> EmbeddedResources = new();


        /// <summary>
        /// Get the last write time of the assembly, if it fails it will return the application start time (EnvInfo.AppStart).
        /// It's safer to assume that the assembly was created at start.
        /// </summary>
        /// <param name="assembly"></param>
        /// <returns></returns>
        public static DateTime GetLastWriteTimerUtc(this Assembly assembly)
        {
            var c = TimeCache;
            if (c.TryGetValue(assembly, out var t))
                return t;
            try
            {
                var loc = assembly.Location;
                if (loc != null)
                {
                    var fi = new FileInfo(loc);
                    if (fi.Exists)
                    {
                        t = fi.LastWriteTimeUtc;
                        c.TryAdd(assembly, t);
                        return t;
                    }
                }
            }
            catch
            {
            }
            t = EnvInfo.AppStart;
            c.TryAdd(assembly, t);
            return t;
        }

        static readonly ConcurrentDictionary<Assembly, DateTime> TimeCache = new ConcurrentDictionary<Assembly, DateTime>();


    }

}
