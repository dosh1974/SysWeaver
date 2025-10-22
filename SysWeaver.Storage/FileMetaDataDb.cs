using SysWeaver.Compression;
using System;
using System.IO;

namespace SysWeaver
{
    /// <summary>
    /// Meta database for a given type
    /// </summary>
    /// <typeparam name="T">The type of the meta data</typeparam>
    public sealed class FileMetaDataDb<T> where T : class, new()
    {

        /// <summary>
        /// Build a database for a given meta data type.
        /// </summary>
        /// <param name="keyType">A unique key for this application, only valid file chars are allowed</param>
        /// <param name="processMetaData">A function that is called to process the data, first argument in the filename supplied, second is the base name to use for any files associated with the meta data. third is the meta data if it exists, return non null to store meta data (typically when the supplied meta data was null)</param>
        /// <param name="cacheExpirationDays">Number of days to keep this meta data around</param>
        /// <param name="keySuffix">Typically a string representation of the parameters, only valid file chars are allowed</param>
        public FileMetaDataDb(String keyType, Func<String, String, T, T> processMetaData, int cacheExpirationDays = 30, String keySuffix = "")
        {
            KeyType = keyType;
            CacheExpirationDays = cacheExpirationDays;
            KeySuffix = String.IsNullOrEmpty(keySuffix) ? "" : ("_" + keySuffix);
            OnData = processMetaData;
        }
        readonly String KeyType;
        readonly int CacheExpirationDays;
        readonly String KeySuffix;

        readonly Func<String, String, T, T> OnData;


        /// <summary>
        /// Process a single file in the db and return it's meta data
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public T Process(String filename)
        {
            var ser = FileMetaData.Serializer;
            var comp= FileMetaData.Compressor;
            var hash = FileHash.GetHash(filename);
            if (hash == null)
                return default(T);

            var folder = FileMetaData.GetTempFolder(filename, KeyType, CacheExpirationDays);
            var destBase = Path.Combine(folder, String.Concat(hash, KeySuffix));
            var cleanMeta = Path.Combine(folder, String.Concat("Meta_", hash, KeySuffix));
            var dataName = cleanMeta + FileMetaData.FileExt;

            using var sysLock = SystemLock.Get(hash);
            var fi = new FileInfo(dataName);
            dataName = fi.FullName;
            T data = null;
            var cache = FileMetaDataDbAsync<T>.Cache;
            if (fi.Exists)
            {
                var lwt = fi.LastWriteTimeUtc;
                if (cache.TryGetValue(dataName, out var ce) && (ce.Item1 == lwt))
                {
                    data = (T)ce.Item2;
                }
                else
                {
                    try
                    {
                        using var ms = new MemoryStream((int)fi.Length * 4);
                        using (var s = fi.OpenRead())
                            comp.Decompress(s, ms);
                        data = ser.Create<T>(ms.GetBuffer().AsSpan().Slice(0, (int)ms.Length));
                        fi.LastAccessTimeUtc = DateTime.UtcNow;
                        cache.TryAdd(dataName, Tuple.Create(lwt, (Object)data));
                    }
                    catch
                    {
                    }
                }
            }
            var pd = data;
            data = OnData(filename, destBase, data);
            if (data == null)
                return pd;
            try
            {
                using (var o = new FileStream(dataName, FileMode.Create))
                    comp.Compress(ser.Serialize(data).Span, o, CompEncoderLevels.Balanced);
                cache.TryAdd(dataName, Tuple.Create(fi.LastWriteTimeUtc, (Object)data));
            }
            catch
            {
                FileMetaData.AdditionalCleanup.Enqueue(destBase);
                FileMetaData.AdditionalCleanup.Enqueue(cleanMeta);
            }
            return data;
        }


    }


}
