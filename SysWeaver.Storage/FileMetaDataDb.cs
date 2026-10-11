using SysWeaver.Compression;
using System;
using System.IO;

namespace SysWeaver
{
    /// <summary>
    /// Meta data database for a given type and key, see <see cref="FileMetaData"/> for details.
    /// </summary>
    /// <typeparam name="T">The type of the meta data (must be json serializable)</typeparam>
    /// <remarks>
    /// Deserialized meta data is cached in memory (shared with <see cref="FileMetaDataDbAsync{T}"/>), the same instance may be returned to many callers so treat it as immutable.
    /// </remarks>
    public sealed class FileMetaDataDb<T> where T : class, new()
    {

        /// <summary>
        /// Build a database for a given meta data type.
        /// </summary>
        /// <param name="keyType">A unique key for this application, only valid file chars are allowed</param>
        /// <param name="processMetaData">A function that is always called (while holding the lock): first argument is the filename supplied, second is the base name (full path without extension) to use for any files associated with the meta data, third is the existing meta data or null.
        /// Return non null to store (replace) the meta data, or null to keep the existing meta data unchanged.</param>
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
        /// Process a single file and return it's meta data.
        /// Reads any existing meta data, calls the process function and stores the result if non null.
        /// Failure to store the meta data is ignored (the associated files are deleted at process exit).
        /// If the key type folder was deleted while the process is running, the process function (or the meta data write) fails with a <see cref="DirectoryNotFoundException"/>:
        /// the folder is then re-created and the failed call is retried once.
        /// </summary>
        /// <param name="filename">The file to process.</param>
        /// <returns>The new meta data if the process function returned non null, else the existing meta data (may be null). Null if the file doesn't exist.</returns>
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

            //  Lock on the key type, key suffix and the content (case insensitive like the file names), so that a callback can process the same content using another key type / suffix (storage-cov-6)
            using var sysLock = SystemLock.Get(String.Concat("FileMetaData_", KeyType, KeySuffix, "_", hash).FastToLower());
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
                    FileMetaData.Touch(fi);
                }
                else
                {
                    try
                    {
                        using var ms = new MemoryStream((int)fi.Length * 4);
                        using (var s = fi.OpenRead())
                            comp.Decompress(s, ms);
                        data = ser.Create<T>(ms.GetBuffer().AsSpan().Slice(0, (int)ms.Length));
                        FileMetaData.Touch(fi);
                        cache[dataName] = Tuple.Create(lwt, (Object)data);
                    }
                    catch
                    {
                    }
                }
            }
            var pd = data;
            try
            {
                data = OnData(filename, destBase, pd);
            }
            catch (DirectoryNotFoundException)
            {
                //  The folder was deleted while the process is running (rare), re-create it and retry once (storage-cov-4)
                PathExt.EnsureFolderExist(folder);
                data = OnData(filename, destBase, pd);
            }
            if (data == null)
                return pd;
            try
            {
                try
                {
                    using (var o = new FileStream(dataName, FileMode.Create))
                        comp.Compress(ser.Serialize(data).Span, o, CompEncoderLevels.Balanced);
                }
                catch (DirectoryNotFoundException)
                {
                    //  The folder was deleted while the process is running (rare), re-create it and retry once (storage-cov-4)
                    PathExt.EnsureFolderExist(folder);
                    using (var o = new FileStream(dataName, FileMode.Create))
                        comp.Compress(ser.Serialize(data).Span, o, CompEncoderLevels.Balanced);
                }
                fi.Refresh();
                cache[dataName] = Tuple.Create(fi.LastWriteTimeUtc, (Object)data);
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
