using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

using SysWeaver.Compression;
using SysWeaver.Serialization;

namespace SysWeaver
{


    /// <summary>
    /// Represent a key value store.
    /// Prefer using the Async methods if possible.
    /// Designed to be as reliable as possible, not for speed:
    /// - Uses system wide locks for data read/writes.
    /// - Uses redundancy by having R copies of the data.
    /// - Uses hashing to validate the data (detects corruption).
    /// - Compresses data (save disc space).
    /// When setting data:
    /// - The (redundancy - 1) oldest (non-existing and invalid copies count as very old) copies are overwritten with the new data.
    /// When reading data:
    /// - The most recent valid copy is returned.
    /// </summary>
    public sealed class KeyValueStore
    {
        /// <summary>
        /// A default key/value store that is the same for all users but application specific
        /// </summary>
        public static readonly KeyValueStore AllApp;

        /// <summary>
        /// A default key/value store that is unique to the users but application specific
        /// </summary>
        public static readonly KeyValueStore UserApp;

        /// <summary>
        /// A default key/value store that is the same for all users and all applications
        /// </summary>
        public static readonly KeyValueStore AllShared;

        /// <summary>
        /// A default key/value store that is unique to the users but common to all applications
        /// </summary>
        public static readonly KeyValueStore UserShared;


        /// <summary>
        /// Get a new custom store, if the store exist, that store is returned
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static KeyValueStore Get(KeyValueStoreParams p)
        {
            p = p ?? new KeyValueStoreParams();
            var id = p.Id;
            if (String.IsNullOrEmpty(id))
                id = "Default";
            var skey = id.FastToLower();
            var s = Stores;
            if (s.TryGetValue(id, out var store))
                return store;
            lock (s)
            {
                if (s.TryGetValue(id, out store))
                    return store;
                store = new KeyValueStore(p);
                if (!s.TryAdd(id, store))
                    throw new Exception("Internal error!");
                return store;
            }
        }


        /// <summary>
        /// Get a value from the key value store
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key">The unqiue key</param>
        /// <param name="returnWhenNotFound">The value to return when the key is not found</param>
        /// <param name="tryAll">If deserialization fails, retry the second most recent copy and so on</param>
        /// <returns>The value in the store, or the supplied default</returns>
        /// <exception cref="Exception"></exception>
        public T TryGet<T>(String key, T returnWhenNotFound = default, bool tryAll = false)
        {
#if DEBUG
            if (PathExt.SafeFilename(key) != key)
                throw new Exception("The key may only contain valid filename chars!");
#endif//DEBUG
            using var lck = SystemLock.Get(LockPrefix + key);
            var f = GetOrderedFiles(key);
            var fl = f.Length;
            while (fl > 0)
            {
                --fl;
                var d = f[fl];
                if (d.Item2 == null)
                    continue;
                var data = TryLoadBytes(d.Item1);
                if (data == null)
                    continue;
                try
                {
                    return Create<T>(data);
                }
                catch
                {
                    if (!tryAll)
                        throw;
                }
            }
            return returnWhenNotFound;
        }

        /// <summary>
        /// Get a value from the key value store
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key">The unqiue key</param>
        /// <param name="returnWhenNotFound">The value to return when the key is not found</param>
        /// <param name="tryAll">If deserialization fails, retry the second most recent copy and so on</param>
        /// <returns>The value in the store, or the supplied default</returns>
        /// <exception cref="Exception"></exception>
        public async Task<T> TryGetAsync<T>(String key, T returnWhenNotFound = default, bool tryAll = false)
        {
#if DEBUG
            if (PathExt.SafeFilename(key) != key)
                throw new Exception("The key may only contain valid filename chars!");
#endif//DEBUG
            using var lck = await SystemLock.GetAsync(LockPrefix + key).ConfigureAwait(false);
            var f = GetOrderedFiles(key);
            var fl = f.Length;
            while (fl > 0)
            {
                --fl;
                var d = f[fl];
                if (d.Item2 == null)
                    continue;
                var data = await TryLoadBytesAsync(d.Item1).ConfigureAwait(false);
                if (data == null)
                    continue;
                try
                {
                    return Create<T>(data);
                }
                catch
                {
                    if (!tryAll)
                        throw;
                }
            }
            return returnWhenNotFound;
        }

                /// <summary>
        /// Set a value in the key value store
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key">The unqiue key</param>
        /// <param name="value">The value to set/replace</param>
        /// <exception cref="Exception"></exception>
        public void Set<T>(String key, T value)
        {
#if DEBUG
            if (PathExt.SafeFilename(key) != key)
                throw new Exception("The key may only contain valid filename chars!");
#endif//DEBUG
            var data = ToData(value);
            using var lck = SystemLock.Get(LockPrefix + key);
            var f = GetOrderedFiles(key, true);
            var fc = f.Length;
            var writeCount = WriteRedundancy;
            while (writeCount < fc)
            {
                if (f[writeCount].Item2 != null)
                    break;
                ++writeCount;
            }
            for (int i = 0; i < writeCount; ++ i)
                FileExt.WriteMemory(f[i].Item1, data, true);

        }

        /// <summary>
        /// Set a value in the key value store
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key">The unqiue key</param>
        /// <param name="value">The value to set/replace</param>
        /// <exception cref="Exception"></exception>
        public async Task SetAsync<T>(String key, T value)
        {
#if DEBUG
            if (PathExt.SafeFilename(key) != key)
                throw new Exception("The key may only contain valid filename chars!");
#endif//DEBUG
            var data = ToData(value);
            using var lck = await SystemLock.GetAsync(LockPrefix + key).ConfigureAwait(false);
            var f = await GetOrderedFilesAsync(key, true).ConfigureAwait(false);
            var fc = f.Length;
            var writeCount = WriteRedundancy;
            while (writeCount < fc)
            {
                if (f[writeCount].Item2 != null)
                    break;
                ++writeCount;
            }
            for (int i = 0; i < writeCount; ++i)
                await FileExt.WriteMemoryAsync(f[i].Item1, data, true).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete a key/value
        /// </summary>
        /// <param name="key">The unqiue key</param>
        /// <exception cref="Exception"></exception>
        public void Delete(String key)
        {
#if DEBUG
            if (PathExt.SafeFilename(key) != key)
                throw new Exception("The key may only contain valid filename chars!");
#endif//DEBUG
            using var lck = SystemLock.Get(LockPrefix + key);
            foreach (var x in Paths)
            {
                var name = Path.Combine(x, key);
                if (File.Exists(name))
                    File.Delete(name);
            }
        }

        /// <summary>
        /// Delete a key/value
        /// </summary>
        /// <param name="key">The unqiue key</param>
        /// <exception cref="Exception"></exception>
        public async Task DeleteAsync(String key)
        {
#if DEBUG
            if (PathExt.SafeFilename(key) != key)
                throw new Exception("The key may only contain valid filename chars!");
#endif//DEBUG
            using var lck = await SystemLock.GetAsync(LockPrefix + key).ConfigureAwait(false);
            foreach (var x in Paths)
            {
                var name = Path.Combine(x, key);
                if (File.Exists(name))
                    File.Delete(name);
            }
        }


        /// <summary>
        /// Id of the store
        /// </summary>
        public readonly String Id;

        /// <summary>
        /// Store redundancy
        /// </summary>
        public int Redundancy => Paths.Length;

#if DEBUG

        public override string ToString() => String.Concat(Id, " using ", Paths.Length, " copies @ ", String.Join(", ", Paths.Select(x => x.ToFolder())));

#endif//DEBUG


        #region Internal
        static KeyValueStore()
        {
            Stores = new ConcurrentDictionary<string, KeyValueStore>(StringComparer.OrdinalIgnoreCase);
            AllApp = Get(new KeyValueStoreParams
            {
                PerApp = true,
                PerUser = false,
            });
            UserApp = Get(new KeyValueStoreParams
            {
                PerApp = true,
                PerUser = true,
            });
            AllShared = Get(new KeyValueStoreParams
            {
                PerApp = false,
                PerUser = false,
            });
            UserShared = Get(new KeyValueStoreParams
            {
                PerApp = false,
                PerUser = true,
            });

        }


        static readonly ConcurrentDictionary<String, KeyValueStore> Stores;
        

        KeyValueStore(KeyValueStoreParams p)
        {
            p = p ?? new KeyValueStoreParams();
            var id = p.Id;
            if (String.IsNullOrEmpty(id))
                id = "Default";
            Id = id;
            Ser = SerManager.Get(p.Ser);
            Comp = String.IsNullOrEmpty(p.Comp) ? null : CompManager.GetFromHttp(p.Comp);
            Level = p.Level;
            var r = p.Redundance;
            if (r < 2)
                r = 2;
            var wr = r - 1;
            var folders = Folders.FromString(p.Folders, Folders.GetBase(p.PerUser, p.PerApp), "KeyValueStore", !p.PerUser);
            var fl = folders.Length;
            while ((r % fl) != 0)
            {
                ++r;
                if (wr < 2)
                    ++wr;
            }
            WriteRedundancy = wr;
            id = PathExt.SafeFilename(id);
            var paths = new String[r];
            for (int i = 0; i < r; ++ i)
            {
                var x = Path.Combine(folders[i % fl], id, i.ToString());
                PathExt.EnsureFolderExist(x);
                paths[i] = x;
            }
            Paths = paths;
            LockPrefix = String.Join('_', "KeyValueStore", HashTools.GetHashString(String.Join(';', paths)));
        }

        public readonly int WriteRedundancy;

        /// <summary>
        /// Get name of all files, ordered from oldest to newest
        /// </summary>
        /// <param name="key"></param>
        /// <param name="validate"></param>
        /// <returns></returns>
        Tuple<String, DateTime?>[] GetOrderedFiles(String key, bool validate = false)
        {
            var p = Paths;
            var pl = p.Length;
            var l = new Tuple<String, DateTime?>[pl];
            for (int i = 0; i < pl; ++ i)
            {
                var name = Path.Combine(p[i], key);
                var fi = new FileInfo(name);
                if (validate && fi.Exists)
                    TryLoadBytes(name);
                l[i] = new Tuple<string, DateTime?>(name, fi.Exists ? fi.LastWriteTimeUtc : null);
            }
            Array.Sort(l, (a, b) =>
            {
                var aa = a.Item2 ?? DateTime.MinValue;
                var bb = b.Item2 ?? DateTime.MinValue;
                var c = aa.CompareTo(bb);
                if (c != 0)
                    return c;
                return a.Item1.CompareTo(b.Item1);
            });
            return l; 
        }


        /// <summary>
        /// Get name of all files, ordered from oldest to newest.
        /// Only use async when validate is true
        /// </summary>
        /// <param name="key"></param>
        /// <param name="validate"></param>
        /// <returns></returns>
        async Task<Tuple<String, DateTime?>[]> GetOrderedFilesAsync(String key, bool validate = false)
        {
            var p = Paths;
            var pl = p.Length;
            var l = new Tuple<String, DateTime?>[pl];
            for (int i = 0; i < pl; ++i)
            {
                var name = Path.Combine(p[i], key);
                var fi = new FileInfo(name);
                if (validate && fi.Exists)
                    await TryLoadBytesAsync(name).ConfigureAwait(false);
                l[i] = new Tuple<string, DateTime?>(name, fi.Exists ? fi.LastWriteTimeUtc : null);
            }
            Array.Sort(l, (a, b) =>
            {
                var aa = a.Item2 ?? DateTime.MinValue;
                var bb = b.Item2 ?? DateTime.MinValue;
                var c = aa.CompareTo(bb);
                if (c != 0)
                    return c;
                return a.Item1.CompareTo(b.Item1);
            });
            return l;
        }

        static bool Validate(Byte[] data)
        {
            if (data == null)
                return false;
            var dl = data.Length - 32;
            if (dl < 1)
                return false;
            var sp = data.AsSpan();
            Span<Byte> hash = stackalloc Byte[32];
            SHA256.HashData(sp.Slice(0, dl), hash);
            return sp.Slice(dl).SequenceEqual(hash);

        }

        static Byte[] TryLoadBytes(String name)
        {
            var data = File.ReadAllBytes(name);
            if (!Validate(data))
            {
                File.Delete(name);
                return null;
            }
            return data;
        }

        static async Task<Byte[]> TryLoadBytesAsync(String name)
        {
            var data = await File.ReadAllBytesAsync(name).ConfigureAwait(false);
            if (!Validate(data))
            {
                File.Delete(name);
                return null;
            }
            return data;
        }

        T Create<T>(Byte[] data)
        {
            ReadOnlySpan<Byte> r = data.AsSpan().Slice(0, data.Length - 32);
            var comp = Comp;
            if (comp != null)
                r = comp.GetDecompressed(r).Span;
            return Ser.Create<T>(r);
        }

        Byte[] ToData<T>(T data)
        {
            var r = Ser.Serialize(data);
            var comp = Comp;
            if (comp != null)
                r = comp.GetCompressed(r.Span, Level);
            var rs = r.Span;
            var l = r.Length;
            var dest = new byte[l + 32];
            var ds = dest.AsSpan();
            rs.CopyTo(ds);
            SHA256.HashData(rs, ds.Slice(l));
            return dest;
        }


        readonly String LockPrefix;
        readonly String[] Paths;

        readonly ISerializerType Ser;
        readonly ICompType Comp;
        readonly CompEncoderLevels Level;


        #endregion//Internal

    }

}
