using SysWeaver.Compression;
using SysWeaver.Serialization;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Configuration;

namespace SysWeaver
{



    /// <summary>
    /// Tools for associating meta data (and derived files, such as compressed copies) with the content of a file, and rebuild it when the file has changed.
    /// </summary>
    /// <remarks>
    /// Meta data is keyed by the content hash of the file (see <see cref="FileHash"/>), so identical files share meta data and any change invalidates it.
    /// The meta data is stored as gzip compressed json in a folder per key type (configurable using "FileMetaDataFolders" and "FileMetaData[KeyType]Folders" in the config),
    /// and is pruned at process exit when it hasn't been used for the configured number of days.
    /// Processing of a given content hash is serialized across processes using a <see cref="SystemLock"/>.
    /// </remarks>
    public static class FileMetaData
    {
        /// <summary>
        /// Read/process some meta data assosicated with a file, if the file is modified in anyway (content hash changes), the meta data is invalidated and the caller should create new data.
        /// </summary>
        /// <typeparam name="T">The data type (must be serializable using json)</typeparam>
        /// <param name="keyType">A unique key for this application, only valid file chars are allowed</param>
        /// <param name="filename">The file to read/process meta data about</param>
        /// <param name="processMetaData">A function that is always called (while holding the lock): first argument is the filename supplied, second is the base name (full path without extension) to use for any files associated with the meta data, third is the existing meta data or null.
        /// Return non null to store (replace) the meta data, or null to keep the existing meta data unchanged.</param>
        /// <param name="cacheExpirationDays">Number of days to keep this meta data around (since last use)</param>
        /// <param name="keySuffix">Typically a string representation of the parameters, only valid file chars are allowed</param>
        /// <returns>The meta data associated with the file, or null if the file doesn't exist</returns>
        public static T Process<T>(String keyType, String filename, Func<String, String, T, T> processMetaData, int cacheExpirationDays = 30, String keySuffix = "") where T : class, new()
        {
            var t = new FileMetaDataDb<T>(String.Join('_', typeof(T).Name, keyType), processMetaData, cacheExpirationDays, keySuffix);
            return t.Process(filename);
        }

        /// <summary>
        /// Read/process some meta data assosicated with a file, if the file is modified in anyway (content hash changes), the meta data is invalidated and the caller should create new data.
        /// </summary>
        /// <typeparam name="T">The data type (must be serializable using json)</typeparam>
        /// <param name="keyType">A unique key for this application, only valid file chars are allowed</param>
        /// <param name="filename">The file to read/process meta data about</param>
        /// <param name="processMetaData">A function that is always called (while holding the lock): first argument is the filename supplied, second is the base name (full path without extension) to use for any files associated with the meta data, third is the existing meta data or null.
        /// Return non null to store (replace) the meta data, or null to keep the existing meta data unchanged.</param>
        /// <param name="cacheExpirationDays">Number of days to keep this meta data around (since last use)</param>
        /// <param name="keySuffix">Typically a string representation of the parameters, only valid file chars are allowed</param>
        /// <returns>The meta data associated with the file, or null if the file doesn't exist</returns>
        public static Task<T> ProcessAsync<T>(String keyType, String filename, Func<String, String, T, Task<T>> processMetaData, int cacheExpirationDays = 30, String keySuffix = "") where T : class, new()
        {
            var t = new FileMetaDataDbAsync<T>(String.Join('_', typeof(T).Name, keyType), processMetaData, cacheExpirationDays, keySuffix);
            return t.ProcessAsync(filename);
        }

        /// <summary>
        /// Read/process some meta data assosicated with a file, if the file is modified in anyway (content hash changes), the meta data is invalidated and the caller should create new data.
        /// </summary>
        /// <typeparam name="T">The data type (must be serializable using json)</typeparam>
        /// <param name="keyType">A unique key for this application, only valid file chars are allowed</param>
        /// <param name="filename">The file to read/process meta data about</param>
        /// <param name="processMetaData">A synchronous function that is always called (while holding the lock): first argument is the filename supplied, second is the base name (full path without extension) to use for any files associated with the meta data, third is the existing meta data or null.
        /// Return non null to store (replace) the meta data, or null to keep the existing meta data unchanged.</param>
        /// <param name="cacheExpirationDays">Number of days to keep this meta data around (since last use)</param>
        /// <param name="keySuffix">Typically a string representation of the parameters, only valid file chars are allowed</param>
        /// <returns>The meta data associated with the file, or null if the file doesn't exist</returns>
        public static Task<T> ProcessAsync<T>(String keyType, String filename, Func<String, String, T, T> processMetaData, int cacheExpirationDays = 30, String keySuffix = "") where T : class, new()
        {
            var t = new FileMetaDataDbAsync<T>(String.Join('_', typeof(T).Name, keyType), processMetaData, cacheExpirationDays, keySuffix);
            return t.ProcessAsync(filename);
        }

        /// <summary>
        /// Get the folder to use for meta data of a given key type and key name.
        /// The first call for a key type (case insensitive) determines the folders and expiration for that key type, and registers pruning at process exit.
        /// </summary>
        /// <param name="keyName">The name used to select one of the configured folders (if there are many).</param>
        /// <param name="keyType">The key type, only valid file chars are allowed.</param>
        /// <param name="cacheExpirationDays">Number of days to keep unused meta data, only used by the first call for a key type.</param>
        /// <returns>The full path of the folder.</returns>
        public static String GetTempFolder(String keyName, String keyType, int cacheExpirationDays = 30)
        {
            var key = keyType.FastToLower();
            var c = CleansUps;
            if (c.TryGetValue(key, out var cc))
                return Folders.SelectFolder(cc.P, keyName);
            lock (c)
            {
                if (c.TryGetValue(key, out cc))
                    return Folders.SelectFolder(cc.P, keyName);
                cc = new CleanUp(keyType, cacheExpirationDays);
                if (!c.TryAdd(key, cc))
                    throw new Exception("Internal error!");
                var p = Folders.SelectFolder(cc.P, keyName);
                return p;
            }
        }

        /// <summary>
        /// The serializer used for meta data files (json).
        /// </summary>
        public static readonly ISerializerType Serializer = SerManager.Get("json");
        /// <summary>
        /// The compressor used for meta data files (gzip).
        /// </summary>
        public static readonly ICompType Compressor = CompManager.GetFromHttp("gzip");
        /// <summary>
        /// The file extension of meta data files, ex: ".json.gz".
        /// </summary>
        public static readonly String FileExt = "." + Serializer.Extension + "." + (Compressor.FileExtensions.FirstOrDefault() ?? Compressor.HttpCode);

        static readonly ConcurrentDictionary<String, CleanUp> CleansUps = new ConcurrentDictionary<string, CleanUp>(StringComparer.Ordinal);

        /// <summary>
        /// Base names (full paths without extension) whose associated files should be deleted at process exit, used when meta data couldn't be saved.
        /// Only processed if at least one key type have been used.
        /// </summary>
        public static readonly ConcurrentQueue<String> AdditionalCleanup = new ConcurrentQueue<string>();

        /// <summary>
        /// Prefix of the meta data files, the rest of the name is the base name (hash + key suffix) followed by FileExt
        /// </summary>
        internal const String MetaPrefix = "Meta_";

        /// <summary>
        /// Length of the content hash that starts all file names (except the meta data prefix)
        /// </summary>
        const int HashLength = 26;

        /// <summary>
        /// Update the last access time of a meta data file (used to decide when to prune it), at most once per hour to avoid excessive file system writes
        /// </summary>
        /// <param name="fi">The meta data file</param>
        internal static void Touch(FileInfo fi)
        {
            try
            {
                var now = DateTime.UtcNow;
                if (fi.LastAccessTimeUtc < now.AddHours(-1))
                    fi.LastAccessTimeUtc = now;
            }
            catch
            {
            }
        }

        static bool IsHash(ReadOnlySpan<char> s)
        {
            if (s.Length < HashLength)
                return false;
            foreach (var c in s.Slice(0, HashLength))
                if (!(((c >= 'a') && (c <= 'z')) || ((c >= '0') && (c <= '9'))))
                    return false;
            return true;
        }

        /// <summary>
        /// Get the base name (hash + key suffix) from a meta data file name
        /// </summary>
        static bool TryGetMetaBase(String fileName, out String baseName)
        {
            baseName = null;
            if (!fileName.StartsWith(MetaPrefix, StringComparison.Ordinal))
                return false;
            if (!fileName.EndsWith(FileExt, StringComparison.OrdinalIgnoreCase))
                return false;
            var b = fileName.Substring(MetaPrefix.Length, fileName.Length - MetaPrefix.Length - FileExt.Length);
            if (!IsHash(b))
                return false;
            baseName = b;
            return true;
        }

        /// <summary>
        /// Find the base name that owns a data file, the longest base name that the file name starts with
        /// (base names of different key suffixes can be prefixes of each other, ex: "hash" and "hash_x").
        /// </summary>
        static String GetOwner(String fileName, IEnumerable<String> baseNames)
        {
            String owner = null;
            foreach (var b in baseNames)
                if (fileName.StartsWith(b, StringComparison.OrdinalIgnoreCase) && ((owner == null) || (b.Length > owner.Length)))
                    owner = b;
            return owner;
        }

        static void TryDelete(FileInfo fi)
        {
            try
            {
                fi.Delete();
            }
            catch
            {
            }
        }

        static DateTime LastUsed(FileInfo fi)
        {
            var a = fi.LastAccessTimeUtc;
            var w = fi.LastWriteTimeUtc;
            return a > w ? a : w;
        }

        /// <summary>
        /// Delete expired meta data and all files associated with it, also deletes old files that aren't associated with any meta data
        /// </summary>
        /// <param name="folder">The folder to clean</param>
        /// <param name="killOlderThan">Meta data that hasn't been used since this time is deleted</param>
        static void CleanFolder(String folder, DateTime killOlderThan)
        {
            var di = new DirectoryInfo(folder);
            if (!di.Exists)
                return;
            var files = di.GetFiles("*", SearchOption.TopDirectoryOnly);
            //  Base name => expired
            var bases = new Dictionary<String, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in files)
                if (TryGetMetaBase(f.Name, out var b))
                    bases[b] = LastUsed(f) < killOlderThan;
            foreach (var f in files)
            {
                var name = f.Name;
                if (TryGetMetaBase(name, out var b))
                {
                    if (bases[b])
                        TryDelete(f);
                    continue;
                }
                if (!IsHash(name))
                    continue;
                var owner = GetOwner(name, bases.Keys);
                //  Files without meta data (ex: old formats, aborted processing) are deleted when they are old
                if (owner == null ? (LastUsed(f) < killOlderThan) : bases[owner])
                    TryDelete(f);
            }
        }

        /// <summary>
        /// Delete all files associated with a base name (used when the meta data couldn't be saved)
        /// </summary>
        /// <param name="baseFileName">The full path of the base name, either the data base name or the meta data name (without extension)</param>
        static void CleanBase(String baseFileName)
        {
            var di = new DirectoryInfo(Path.GetDirectoryName(baseFileName));
            if (!di.Exists)
                return;
            var name = Path.GetFileName(baseFileName);
            if (name.StartsWith(MetaPrefix, StringComparison.Ordinal))
            {
                TryDelete(new FileInfo(baseFileName + FileExt));
                return;
            }
            var files = di.GetFiles("*", SearchOption.TopDirectoryOnly);
            var bases = new HashSet<String>(StringComparer.OrdinalIgnoreCase) { name };
            foreach (var f in files)
                if (TryGetMetaBase(f.Name, out var b))
                    bases.Add(b);
            foreach (var f in files)
            {
                if (f.Name.StartsWith(MetaPrefix, StringComparison.Ordinal))
                    continue;
                if (String.Equals(GetOwner(f.Name, bases), name, StringComparison.OrdinalIgnoreCase))
                    TryDelete(f);
            }
        }

        /// <summary>
        /// The resolved folders (P) and the negative expiration in days (C) of a key type, registers pruning of the folders at process exit.
        /// </summary>
        sealed class CleanUp
        {
            public readonly String[] P;
            public readonly int C;

            public CleanUp(String keyType, int cacheExpirationDays = 30)
            {
                C = -Math.Max(1, cacheExpirationDays);

                var baseFolder = Folders.FromConfig("FileMetaDataFolders", Folders.AllSharedFolders, "FileMetaData", true);
                var p = Folders.FromConfig("FileMetaData" + keyType + "Folders", baseFolder, keyType, true);
                P = p;
                AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            }

            void CurrentDomain_ProcessExit(object sender, EventArgs e)
            {
                var killOlderThan = DateTime.UtcNow.AddDays(C);
                foreach (var p in P)
                {
                    try
                    {
                        CleanFolder(p, killOlderThan);
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
                        CleanBase(bn);
                    }
                    catch
                    {
                    }
                }
            }
        }
    }


}
