using System;
using System.Collections.Concurrent;
using System.IO;

namespace SysWeaver
{
    public static class TempFolder
    {
        /// <summary>
        /// Get the name to a temporary files folder (cache)
        /// </summary>
        /// <param name="keyType">A unique name for this temp folder</param>
        /// <param name="cacheExpirationDays">Number of days to keep files</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
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
                var p = cc.P;
                PathExt.AllowAllAccess(p);
                return p;
            }
        }


        /// <summary>
        /// Add files that should be deleted on exit here
        /// </summary>
        /// <param name="s"></param>
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
                p = p ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SysWeaver_" + keyType);
                P = PathExt.RootExecutable(p);
            }

        }

    }
}
