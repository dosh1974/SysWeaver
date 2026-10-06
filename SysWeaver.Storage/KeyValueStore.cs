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
    /// Represent a file based key value store.
    /// Prefer using the Async methods if possible.
    /// Designed to be as reliable as possible, not for speed:
    /// - Uses system wide locks (<see cref="SystemLock"/>, one per key) for data read/writes, so it's safe to use from multiple threads and processes.
    /// - Uses redundancy by having R copies of the data (one file per copy, in separate sub folders and optionally on separate volumes).
    /// - Uses a SHA256 checksum to validate the data (detects corruption, invalid copies are deleted when found).
    /// - Compresses data (save disc space).
    /// When setting data:
    /// - The <see cref="WriteRedundancy"/> oldest copies (non-existing and invalid copies count as very old) are overwritten with the new data.
    /// When reading data:
    /// - The most recent valid copy is returned.
    /// </summary>
    /// <remarks>
    /// The key is used as a file name, so it may only contain valid file name characters (only verified in DEBUG builds, never pass untrusted keys).
    /// Values are written with the configured serializer, so the same type should be used when reading.
    /// Not intended for large values or high write rates, every operation touches all copies.
    /// </remarks>
    public sealed class KeyValueStore
    {
        /// <summary>
        /// A default key/value store that is the same for all users but application specific.
        /// </summary>
        /// <remarks>
        /// All four default stores are created with the id "Default", and <see cref="Get(KeyValueStoreParams)"/> caches stores by id,
        /// so currently <see cref="UserApp"/>, <see cref="AllShared"/> and <see cref="UserShared"/> return this same instance (all data is stored per application for all users).
        /// </remarks>
        public static readonly KeyValueStore AllApp;

        /// <summary>
        /// A default key/value store that is intended to be unique to the user but application specific.
        /// Note: currently the same instance as <see cref="AllApp"/> (see the remarks there).
        /// </summary>
        public static readonly KeyValueStore UserApp;

        /// <summary>
        /// A default key/value store that is intended to be the same for all users and all applications.
        /// Note: currently the same instance as <see cref="AllApp"/> (see the remarks there).
        /// </summary>
        public static readonly KeyValueStore AllShared;

        /// <summary>
        /// A default key/value store that is intended to be unique to the user but common to all applications.
        /// Note: currently the same instance as <see cref="AllApp"/> (see the remarks there).
        /// </summary>
        public static readonly KeyValueStore UserShared;


        /// <summary>
        /// Get a custom store, creating it if needed.
        /// Stores are cached by <see cref="KeyValueStoreParams.Id"/> (case insensitive), if a store with that id exist, that store is returned and the other parameters are ignored.
        /// </summary>
        /// <param name="p">The store parameters, null uses the defaults (id "Default").</param>
        /// <returns>The store.</returns>
        /// <exception cref="Exception">The serializer or folders couldn't be resolved, or the store folders couldn't be created.</exception>
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
        /// Get a value from the key value store, blocking while waiting for the key lock.
        /// </summary>
        /// <typeparam name="T">The type of the value (should be the same type as used when setting it)</typeparam>
        /// <param name="key">The unique key, may only contain valid file name characters</param>
        /// <param name="returnWhenNotFound">The value to return when the key is not found (or no valid copy exists)</param>
        /// <param name="tryAll">If deserialization fails, retry the second most recent copy and so on</param>
        /// <returns>The value in the store, or the supplied default</returns>
        /// <exception cref="Exception">Deserialization of the most recent valid copy failed and <paramref name="tryAll"/> is false.</exception>
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
                using var data = TryLoadBytes(d.Item1);
                if (data == null)
                    continue;
                try
                {
                    return Create<T>(data.Memory);
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
        /// Get a value from the key value store.
        /// </summary>
        /// <typeparam name="T">The type of the value (should be the same type as used when setting it)</typeparam>
        /// <param name="key">The unique key, may only contain valid file name characters</param>
        /// <param name="returnWhenNotFound">The value to return when the key is not found (or no valid copy exists)</param>
        /// <param name="tryAll">If deserialization fails, retry the second most recent copy and so on</param>
        /// <returns>The value in the store, or the supplied default</returns>
        /// <exception cref="Exception">Deserialization of the most recent valid copy failed and <paramref name="tryAll"/> is false.</exception>
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
                using var data = await TryLoadBytesAsync(d.Item1).ConfigureAwait(false);
                if (data == null)
                    continue;
                try
                {
                    return Create<T>(data.Memory);
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
        /// Set a value in the key value store, blocking while waiting for the key lock.
        /// All existing copies are read and validated before the oldest / invalid copies are overwritten.
        /// </summary>
        /// <typeparam name="T">The type of the value</typeparam>
        /// <param name="key">The unique key, may only contain valid file name characters</param>
        /// <param name="value">The value to set/replace</param>
        /// <exception cref="Exception">Serialization or writing to disc failed.</exception>
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
        /// Set a value in the key value store.
        /// All existing copies are read and validated before the oldest / invalid copies are overwritten.
        /// </summary>
        /// <typeparam name="T">The type of the value</typeparam>
        /// <param name="key">The unique key, may only contain valid file name characters</param>
        /// <param name="value">The value to set/replace</param>
        /// <exception cref="Exception">Serialization or writing to disc failed.</exception>
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
        /// Delete a key/value (all copies), blocking while waiting for the key lock.
        /// Deleting a key that doesn't exist does nothing.
        /// </summary>
        /// <param name="key">The unique key, may only contain valid file name characters</param>
        /// <exception cref="Exception">A copy couldn't be deleted.</exception>
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
        /// Delete a key/value (all copies).
        /// Deleting a key that doesn't exist does nothing.
        /// </summary>
        /// <param name="key">The unique key, may only contain valid file name characters</param>
        /// <exception cref="Exception">A copy couldn't be deleted.</exception>
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
        /// Store redundancy, the number of copies (files) kept for each key.
        /// </summary>
        public int Redundancy => Paths.Length;

#if DEBUG

        /// <inheritdoc/>
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

        /// <summary>
        /// The minimum number of copies that are overwritten on every write (normally <see cref="Redundancy"/> - 1, so the previous value survives a failed write).
        /// </summary>
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
                var valid = fi.Exists;
                if (validate && valid)
                {
                    using var data = TryLoadBytes(name);
                    valid = data != null;
                }
                l[i] = new Tuple<string, DateTime?>(name, valid? fi.LastWriteTimeUtc : null);
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
                var valid = fi.Exists;
                if (validate && valid)
                {
                    using var data = await TryLoadBytesAsync(name).ConfigureAwait(false);
                    valid = data != null;
                }
                l[i] = new Tuple<string, DateTime?>(name, valid ? fi.LastWriteTimeUtc : null);
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

        static bool Validate(ReadOnlyMemory<Byte> data)
        {
            var dl = data.Length - 32;
            if (dl < 1)
                return false;
            var sp = data.Span;
            Span<Byte> hash = stackalloc Byte[32];
            SHA256.HashData(sp.Slice(0, dl), hash);
            return sp.Slice(dl).SequenceEqual(hash);

        }

        static IUnmanagedReadOnlyMemory<Byte> TryLoadBytes(String name)
        {
            var data = FileReadOnlyMemory.Read(name);
            if (!Validate(data.Memory))
            {
                data.Dispose();
                File.Delete(name);
                return null;
            }
            return data;
        }

        static async Task<IUnmanagedReadOnlyMemory<Byte>> TryLoadBytesAsync(String name)
        {
            var data = await FileReadOnlyMemory.ReadAsync(name).ConfigureAwait(false);
            if (data == null || (!Validate(data.Memory)))
            {
                data?.Dispose();
                await PathExt.TryDeleteFileAsync(name).ConfigureAwait(false);
                return null;
            }
            return data;
        }

        T Create<T>(ReadOnlyMemory<Byte> data)
        {
            ReadOnlySpan<Byte> r = data.Span.Slice(0, data.Length - 32);
            var comp = Comp;
            if (comp != null)
            {
                using var t = comp.GetUnmanagedDecompressed(r);
                return Ser.Create<T>(t.Memory);
            }
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
            var dest = GC.AllocateUninitializedArray<Byte>(l + 32);
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
