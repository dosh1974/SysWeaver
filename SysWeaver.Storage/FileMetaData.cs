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
    /// Tools for associating meta data with a file and rebuild it when the file has changed.
    /// </summary>
    public static class FileMetaData
    {
        /// <summary>
        /// Read/process some meta data assosicated with a file, if the file is modified in anyway (content hash changes), the meta data is invalidated and the caller should create new data.
        /// </summary>
        /// <typeparam name="T">The data type (must be serializable using json)</typeparam>
        /// <param name="keyType">A unique key for this application, only valid file chars are allowed</param>
        /// <param name="filename">The file to read/process meta data about</param>
        /// <param name="processMetaData">A function that is called to process the data, first argument in the filename supplied, second is the base name to use for any files associated with the meta data. third is the meta data if it exists, return non null to store meta data (typically when the supplied meta data was null)</param>
        /// <param name="cacheExpirationDays">Number of days to keep this meta data around</param>
        /// <param name="keySuffix">Typically a string representation of the parameters, only valid file chars are allowed</param>
        /// <returns>The meta data associated with the file</returns>
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
        /// <param name="processMetaData">A function that is called to process the data, first argument in the filename supplied, second is the base name to use for any files associated with the meta data. third is the meta data if it exists, return non null to store meta data (typically when the supplied meta data was null)</param>
        /// <param name="cacheExpirationDays">Number of days to keep this meta data around</param>
        /// <param name="keySuffix">Typically a string representation of the parameters, only valid file chars are allowed</param>
        /// <returns>The meta data associated with the file</returns>
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
        /// <param name="processMetaData">A function that is called to process the data, first argument in the filename supplied, second is the base name to use for any files associated with the meta data. third is the meta data if it exists, return non null to store meta data (typically when the supplied meta data was null)</param>
        /// <param name="cacheExpirationDays">Number of days to keep this meta data around</param>
        /// <param name="keySuffix">Typically a string representation of the parameters, only valid file chars are allowed</param>
        /// <returns>The meta data associated with the file</returns>
        public static Task<T> ProcessAsync<T>(String keyType, String filename, Func<String, String, T, T> processMetaData, int cacheExpirationDays = 30, String keySuffix = "") where T : class, new()
        {
            var t = new FileMetaDataDbAsync<T>(String.Join('_', typeof(T).Name, keyType), processMetaData, cacheExpirationDays, keySuffix);
            return t.ProcessAsync(filename);
        }

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

        public static readonly ISerializerType Serializer = SerManager.Get("json");
        public static readonly ICompType Compressor = CompManager.GetFromHttp("gzip");
        public static readonly String FileExt = "." + Serializer.Extension + "." + (Compressor.FileExtensions.FirstOrDefault() ?? Compressor.HttpCode);

        static readonly ConcurrentDictionary<String, CleanUp> CleansUps = new ConcurrentDictionary<string, CleanUp>(StringComparer.Ordinal);

        public static readonly ConcurrentQueue<String> AdditionalCleanup = new ConcurrentQueue<string>();

        sealed class CleanUp
        {
            public readonly String[] P;
            public readonly int C;

            public CleanUp(String keyType, int cacheExpirationDays = 30)
            {
                C = -Math.Max(1, cacheExpirationDays);

                var baeFolder = Folders.FromConfig("FileMetaDataFolders", Folders.AllSharedFolders, "FileMetaData");
                var p = Folders.FromConfig("FileMetaData" + keyType + "Folders", baeFolder, keyType);
                P = p;
                AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            }

            void CurrentDomain_ProcessExit(object sender, EventArgs e)
            {
                try
                {
                    var killOlderThan = DateTime.UtcNow.AddDays(C);
                    foreach (var p in P)
                    {
                        var fl = p.Length + 27 + FileExt.Length;
                        foreach (var x in Directory.GetFiles(p, "*" + FileExt, SearchOption.TopDirectoryOnly))
                        {
                            if (x.Length != fl)
                                continue;
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
                                    foreach (var y in Directory.GetFiles(p, fi.Name + "_*", SearchOption.TopDirectoryOnly))
                                    {
                                        try
                                        {
                                            File.Delete(y);
                                        }
                                        catch
                                        {
                                        }
                                    }
                                }
                            }
                            catch
                            {
                            }
                        }
                    }
                }
                catch
                {
                }
                var ac = AdditionalCleanup;
                while (ac.TryDequeue(out var bn))
                {
                    try
                    {
                        foreach (var y in Directory.GetFiles(Path.GetDirectoryName(bn), Path.GetFileNameWithoutExtension(bn) + "_*", SearchOption.TopDirectoryOnly))
                        {
                            try
                            {
                                File.Delete(y);
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
        }
    }


}
