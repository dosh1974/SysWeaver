using SysWeaver.Compression;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace SysWeaver
{
    public sealed class FileMetaDataDbAsync<T> where T : class, new()
    {

        /// <summary>
        /// Build a database for a given meta data type.
        /// </summary>
        /// <param name="keyType">A unique key for this application, only valid file chars are allowed</param>
        /// <param name="processMetaData">A function that is called to process the data, first argument in the filename supplied, second is the base name to use for any files associated with the meta data. third is the meta data if it exists, return non null to store meta data (typically when the supplied meta data was null)</param>
        /// <param name="cacheExpirationDays">Number of days to keep this meta data around</param>
        /// <param name="keySuffix">Typically a string representation of the parameters, only valid file chars are allowed</param>
        public FileMetaDataDbAsync(String keyType, Func<String, String, T, Task<T>> processMetaData, int cacheExpirationDays = 30, String keySuffix = "")
        {
            KeyType = keyType;
            CacheExpirationDays = cacheExpirationDays;
            KeySuffix = String.IsNullOrEmpty(keySuffix) ? "" : ("_" + keySuffix);
            OnDataAsync = processMetaData;
        }

        /// <summary>
        /// Build a database for a given meta data type.
        /// </summary>
        /// <param name="keyType">A unique key for this application, only valid file chars are allowed</param>
        /// <param name="processMetaData">A function that is called to process the data, first argument in the filename supplied, second is the base name to use for any files associated with the meta data. third is the meta data if it exists, return non null to store meta data (typically when the supplied meta data was null)</param>
        /// <param name="cacheExpirationDays">Number of days to keep this meta data around</param>
        /// <param name="keySuffix">Typically a string representation of the parameters, only valid file chars are allowed</param>
        public FileMetaDataDbAsync(String keyType, Func<String, String, T, T> processMetaData, int cacheExpirationDays = 30, String keySuffix = "")
        {
            KeyType = keyType;
            CacheExpirationDays = cacheExpirationDays;
            KeySuffix = String.IsNullOrEmpty(keySuffix) ? "" : ("_" + keySuffix);
            OnData = processMetaData;
        }

        readonly String KeyType;
        readonly int CacheExpirationDays;

        readonly String KeySuffix;

        readonly Func<String, String, T, Task<T>> OnDataAsync;
        readonly Func<String, String, T, T> OnData;



        internal static readonly ConcurrentDictionary<String, Tuple<DateTime, Object>> Cache = new ConcurrentDictionary<string, Tuple<DateTime, object>>(StringComparer.Ordinal);

        /// <summary>
        /// Process a single file in the db and return it's meta data
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public async Task<T> ProcessAsync(String filename)
        {
            var ser = FileMetaData.Serializer;
            var comp = FileMetaData.Compressor;
            var hash = await FileHash.GetHashAsync(filename).ConfigureAwait(false);
            if (hash == null)
                return default(T);


            var folder = FileMetaData.GetTempFolder(filename, KeyType, CacheExpirationDays);
            var destBase = Path.Combine(folder, String.Concat(hash, KeySuffix));
            var cleanMeta = Path.Combine(folder, String.Concat("Meta_", hash, KeySuffix));
            var dataName = cleanMeta + FileMetaData.FileExt;

            using var sysLock = await SystemLock.GetAsync(hash).ConfigureAwait(false);
            var fi = new FileInfo(dataName);
            dataName = fi.FullName;
            T data = null;
            var cache = Cache;
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
                            await comp.DecompressAsync(s, ms).ConfigureAwait(false);
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
            var ad = OnDataAsync;
            if (ad == null)
            {
                data = OnData(filename, destBase, data);
            }
            else
            {
                data = await ad(filename, destBase, data).ConfigureAwait(false);
            }
            if (data == null)
                return pd;
            try
            {
                using (var o = new FileStream(dataName, FileMode.Create))
                    await comp.CompressAsync(ser.Serialize(data), o, CompEncoderLevels.Balanced).ConfigureAwait(false);
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
