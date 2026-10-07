using System;
using System.Collections.Concurrent;
using System.IO;

namespace SysWeaver
{
    /// <summary>
    /// Named folders for temporary / cached files, with automatic pruning of old files at process exit.
    /// </summary>
    /// <remarks>
    /// The folder for a key type is (in priority order): the config value "TempFolder.Folder.[keyType]", "[TempFolder.Folder]/SysWeaver_[keyType]"
    /// or "[CommonApplicationData]/SysWeaver_[keyType]". Relative paths are relative to the executable folder.
    /// At process exit, files (top folder only) whose last access time is older than the expiration are deleted.
    /// </remarks>
    public static class TempFolder
    {
        /// <summary>
        /// Get the full path to a temporary files folder (cache), creating it if needed.
        /// The first call for a key type (case insensitive) determines the folder and expiration.
        /// </summary>
        /// <param name="keyType">A unique name for this temp folder, only valid file name chars are allowed</param>
        /// <param name="cacheExpirationDays">Number of days to keep files (since last access), zero or less disables pruning</param>
        /// <returns>The full path of the folder</returns>
        public static String Get(String keyType, int cacheExpirationDays = 30)
        {
            var key = keyType.FastToLower();
            var c = CleansUps;
            if (c.TryGetValue(key, out var cc))
                return cc.P;
            lock (c)
            {
                if (c.TryGetValue(key, out cc))
                    return cc.P;
                cc = new CleanUp(keyType, cacheExpirationDays);
                if (!c.TryAdd(key, cc))
                    throw new Exception("Internal error!");
                return cc.P;
            }
        }


        /// <summary>
        /// Add a file that should be deleted at process exit (errors are ignored).
        /// </summary>
        /// <param name="s">The full path of the file to delete</param>
        public static void DeleteOnExit(String s) => AdditionalCleanup.Enqueue(s);


        static readonly ConcurrentDictionary<String, CleanUp> CleansUps = new ConcurrentDictionary<string, CleanUp>(StringComparer.Ordinal);

        static readonly ConcurrentQueue<String> AdditionalCleanup = new ConcurrentQueue<string>();


        static TempFolder()
        {
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
        }

        static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            foreach (var folder in CleansUps.Values)
            {
                try
                {
                    var c = folder.C;
                    if (c <= 0)
                        continue;
                    var killOlderThan = DateTime.UtcNow.AddDays(c);
                    var p = folder.P;
                    foreach (var x in Directory.GetFiles(p, "*.*", SearchOption.TopDirectoryOnly))
                    {
                        try
                        {
                            var fi = new FileInfo(x);
                            if (!fi.Exists)
                                continue;
                            if (fi.LastAccessTimeUtc < killOlderThan)
                            {
                                try
                                {
                                    fi.Delete();
                                }
                                catch
                                {
                                }
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
            }
            var ac = AdditionalCleanup;
            while (ac.TryDequeue(out var bn))
            {
                try
                {
                    File.Delete(bn);
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// The resolved folder (P) and the negative expiration in days (C, 0 means no pruning) of a key type.
        /// </summary>
        sealed class CleanUp
        {
            public readonly String P;
            public readonly int C;

            public CleanUp(String keyType, int cacheExpirationDays = 30)
            {
                C = cacheExpirationDays <= 0 ? 0 : -Math.Max(1, cacheExpirationDays);

                var p = Config.GetString(nameof(TempFolder) + ".Folder." + keyType);
                if (p == null)
                {
                    p = Config.GetString(nameof(TempFolder) + ".Folder");
                    if (p != null)
                        p = Path.Combine(p, "SysWeaver_" + keyType);
                }
                p = p ?? Path.Combine(PathTemplate.CommonApplicationData, "SysWeaver_" + keyType);
                P = PathExt.RootExecutable(p);
                PathExt.CreateDataFolder(P);

            }

        }

    }
}
