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
    /// The key is used as the file name of the copies, so it MUST be a valid file name: not null or empty, not "." or "..", and no invalid file name chars
    /// (no path separators or ':', on Windows also no '*', '?', '"', '&lt;', '&gt;', '|' or control chars), i.e. <see cref="PathExt.SafeFilename(string)"/> must return the key unchanged.
    /// This is validated in all builds, an <see cref="ArgumentException"/> is thrown for invalid keys (so a key can't be used to access files outside the store).
    /// Keys are never transformed, since changing the file name would make existing data unreadable: callers that build keys from arbitrary text must make them valid file names
    /// (ex: using <see cref="PathExt.SafeFilename(string)"/>).
    /// Values are written with the configured serializer, so the same type should be used when reading.
    /// Not intended for large values or high write rates, every operation touches all copies.
    /// </remarks>
    public sealed class KeyValueStore
    {
        /// <summary>
        /// A default key/value store that is the same for all users but application specific.
        /// This is the store with the id "Default" (returned by <see cref="Get(KeyValueStoreParams)"/> for the id "Default"), stored in <see cref="Folders.AllAppFolders"/>.
        /// </summary>
        /// <remarks>
        /// Before 2026-10-07 <see cref="UserApp"/>, <see cref="AllShared"/> and <see cref="UserShared"/> were the same instance as this store,
        /// so their data was written to this store's location. Those stores read keys that they don't have from this store (see <see cref="UserApp"/>).
        /// </remarks>
        public static readonly KeyValueStore AllApp;

        /// <summary>
        /// A default key/value store that is unique to the user but application specific, stored in <see cref="Folders.UserAppFolders"/>.
        /// </summary>
        /// <remarks>
        /// Legacy fallback (applies to <see cref="UserApp"/>, <see cref="AllShared"/> and <see cref="UserShared"/>):
        /// Before 2026-10-07 these stores were the same instance as <see cref="AllApp"/>, so existing data is in the <see cref="AllApp"/> location.
        /// When a key has no valid copy in this store, the value is read from <see cref="AllApp"/> and copied to this store (the <see cref="AllApp"/> copy is kept, since <see cref="AllApp"/> may use the same key).
        /// Deleting a key from this store stops the fallback for that key (a marker is written), but doesn't delete the <see cref="AllApp"/> copy.
        /// Note: <see cref="AllApp"/> is common to all users, so (as before) a value written to a per user store by older versions is visible to all users until it's set in their own store.
        /// If the store folders can't be created, this is the <see cref="AllApp"/> instance (the old behaviour).
        /// </remarks>
        public static readonly KeyValueStore UserApp;

        /// <summary>
        /// A default key/value store that is the same for all users and all applications, stored in <see cref="Folders.AllSharedFolders"/>.
        /// Keys that aren't found are read from (and copied from) <see cref="AllApp"/>, see the remarks on <see cref="UserApp"/>.
        /// </summary>
        public static readonly KeyValueStore AllShared;

        /// <summary>
        /// A default key/value store that is unique to the user but common to all applications, stored in <see cref="Folders.UserSharedFolders"/>.
        /// Keys that aren't found are read from (and copied from) <see cref="AllApp"/>, see the remarks on <see cref="UserApp"/>.
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
        /// <param name="key">The unique key, MUST be a valid file name (it's used as the file name, no path separators, ':' etc, see the remarks on <see cref="KeyValueStore"/>)</param>
        /// <exception cref="ArgumentException"><paramref name="key"/> is null, empty, "." or "..", or contains invalid file name chars (validated in all builds).</exception>
        /// <param name="returnWhenNotFound">The value to return when the key is not found (or no valid copy exists)</param>
        /// <param name="tryAll">If deserialization fails, retry the second most recent copy and so on</param>
        /// <returns>The value in the store (for <see cref="UserApp"/>, <see cref="AllShared"/> and <see cref="UserShared"/> possibly read from <see cref="AllApp"/>, see the remarks on <see cref="UserApp"/>), or the supplied default</returns>
        /// <exception cref="Exception">Deserialization of the most recent valid copy failed and <paramref name="tryAll"/> is false.</exception>
        public T TryGet<T>(String key, T returnWhenNotFound = default, bool tryAll = false)
        {
            ValidateKey(key);
            using var lck = SystemLock.Get(LockPrefix + key);
            var f = GetOrderedFiles(key);
            var fl = f.Length;
            bool anyValid = false;
            while (fl > 0)
            {
                --fl;
                var d = f[fl];
                if (d.Item2 == null)
                    continue;
                using var data = TryLoadBytes(d.Item1);
                if (data == null)
                    continue;
                anyValid = true;
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
            if (anyValid || (Legacy == null))
                return returnWhenNotFound;
            return TryGetLegacy(key, returnWhenNotFound, tryAll);
        }

        /// <summary>
        /// Get a value from the key value store.
        /// </summary>
        /// <typeparam name="T">The type of the value (should be the same type as used when setting it)</typeparam>
        /// <param name="key">The unique key, MUST be a valid file name (it's used as the file name, no path separators, ':' etc, see the remarks on <see cref="KeyValueStore"/>)</param>
        /// <exception cref="ArgumentException"><paramref name="key"/> is null, empty, "." or "..", or contains invalid file name chars (validated in all builds).</exception>
        /// <param name="returnWhenNotFound">The value to return when the key is not found (or no valid copy exists)</param>
        /// <param name="tryAll">If deserialization fails, retry the second most recent copy and so on</param>
        /// <returns>The value in the store (for <see cref="UserApp"/>, <see cref="AllShared"/> and <see cref="UserShared"/> possibly read from <see cref="AllApp"/>, see the remarks on <see cref="UserApp"/>), or the supplied default</returns>
        /// <exception cref="Exception">Deserialization of the most recent valid copy failed and <paramref name="tryAll"/> is false.</exception>
        public async Task<T> TryGetAsync<T>(String key, T returnWhenNotFound = default, bool tryAll = false)
        {
            ValidateKey(key);
            using var lck = await SystemLock.GetAsync(LockPrefix + key).ConfigureAwait(false);
            var f = GetOrderedFiles(key);
            var fl = f.Length;
            bool anyValid = false;
            while (fl > 0)
            {
                --fl;
                var d = f[fl];
                if (d.Item2 == null)
                    continue;
                using var data = await TryLoadBytesAsync(d.Item1).ConfigureAwait(false);
                if (data == null)
                    continue;
                anyValid = true;
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
            if (anyValid || (Legacy == null))
                return returnWhenNotFound;
            return await TryGetLegacyAsync(key, returnWhenNotFound, tryAll).ConfigureAwait(false);
        }

        /// <summary>
        /// Set a value in the key value store, blocking while waiting for the key lock.
        /// All existing copies are read and validated before the oldest / invalid copies are overwritten.
        /// </summary>
        /// <typeparam name="T">The type of the value</typeparam>
        /// <param name="key">The unique key, MUST be a valid file name (it's used as the file name, no path separators, ':' etc, see the remarks on <see cref="KeyValueStore"/>)</param>
        /// <exception cref="ArgumentException"><paramref name="key"/> is null, empty, "." or "..", or contains invalid file name chars (validated in all builds).</exception>
        /// <param name="value">The value to set/replace</param>
        /// <exception cref="Exception">Serialization or writing to disc failed.</exception>
        public void Set<T>(String key, T value)
        {
            ValidateKey(key);
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
            EnsureNewest(f, writeCount);

        }

        /// <summary>
        /// Set a value in the key value store.
        /// All existing copies are read and validated before the oldest / invalid copies are overwritten.
        /// </summary>
        /// <typeparam name="T">The type of the value</typeparam>
        /// <param name="key">The unique key, MUST be a valid file name (it's used as the file name, no path separators, ':' etc, see the remarks on <see cref="KeyValueStore"/>)</param>
        /// <exception cref="ArgumentException"><paramref name="key"/> is null, empty, "." or "..", or contains invalid file name chars (validated in all builds).</exception>
        /// <param name="value">The value to set/replace</param>
        /// <exception cref="Exception">Serialization or writing to disc failed.</exception>
        public async Task SetAsync<T>(String key, T value)
        {
            ValidateKey(key);
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
            EnsureNewest(f, writeCount);
        }

        /// <summary>
        /// Delete a key/value (all copies), blocking while waiting for the key lock.
        /// Deleting a key that doesn't exist does nothing.
        /// For <see cref="UserApp"/>, <see cref="AllShared"/> and <see cref="UserShared"/> the legacy copy in <see cref="AllApp"/> (if any) is kept, but no longer read by this store.
        /// </summary>
        /// <param name="key">The unique key, MUST be a valid file name (it's used as the file name, no path separators, ':' etc, see the remarks on <see cref="KeyValueStore"/>)</param>
        /// <exception cref="ArgumentException"><paramref name="key"/> is null, empty, "." or "..", or contains invalid file name chars (validated in all builds).</exception>
        /// <exception cref="Exception">A copy couldn't be deleted.</exception>
        public void Delete(String key)
        {
            ValidateKey(key);
            using var lck = SystemLock.Get(LockPrefix + key);
            foreach (var x in Paths)
            {
                var name = Path.Combine(x, key);
                if (File.Exists(name))
                    File.Delete(name);
            }
            MarkLegacyDeleted(key);
        }

        /// <summary>
        /// Delete a key/value (all copies).
        /// Deleting a key that doesn't exist does nothing.
        /// For <see cref="UserApp"/>, <see cref="AllShared"/> and <see cref="UserShared"/> the legacy copy in <see cref="AllApp"/> (if any) is kept, but no longer read by this store.
        /// </summary>
        /// <param name="key">The unique key, MUST be a valid file name (it's used as the file name, no path separators, ':' etc, see the remarks on <see cref="KeyValueStore"/>)</param>
        /// <exception cref="ArgumentException"><paramref name="key"/> is null, empty, "." or "..", or contains invalid file name chars (validated in all builds).</exception>
        /// <exception cref="Exception">A copy couldn't be deleted.</exception>
        public async Task DeleteAsync(String key)
        {
            ValidateKey(key);
            using var lck = await SystemLock.GetAsync(LockPrefix + key).ConfigureAwait(false);
            foreach (var x in Paths)
            {
                var name = Path.Combine(x, key);
                if (File.Exists(name))
                    File.Delete(name);
            }
            MarkLegacyDeleted(key);
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

        /// <summary>
        /// Validate that a key is a valid file name (always, not only in DEBUG builds), since the key is used as a file name.
        /// </summary>
        /// <param name="key">The key to validate</param>
        /// <exception cref="ArgumentException">The key is null, empty, "." or "..", or contains invalid file name chars.</exception>
        static void ValidateKey(String key)
        {
            if (String.IsNullOrEmpty(key))
                throw new ArgumentException("The key may not be null or empty!", nameof(key));
            if ((key == ".") || (key == ".."))
                throw new ArgumentException("The key \"" + key + "\" isn't a valid file name!", nameof(key));
            if (!ReferenceEquals(PathExt.SafeFilename(key), key))
                throw new ArgumentException("The key \"" + key + "\" may only contain valid file name chars (it's used as a file name)!", nameof(key));
        }

        static KeyValueStore()
        {
            Stores = new ConcurrentDictionary<string, KeyValueStore>(StringComparer.OrdinalIgnoreCase);
            AllApp = Get(new KeyValueStoreParams
            {
                PerApp = true,
                PerUser = false,
            });
            UserApp = CreateDefault(true, true);
            AllShared = CreateDefault(false, false);
            UserShared = CreateDefault(false, true);
        }

        /// <summary>
        /// Create one of the default stores (not cached by id, so <see cref="Get(KeyValueStoreParams)"/> with the id "Default" still returns <see cref="AllApp"/>).
        /// Uses <see cref="AllApp"/> as the legacy store (where the data was written before 2026-10-07).
        /// </summary>
        /// <param name="perApp">Per application or shared between all applications</param>
        /// <param name="perUser">Per user or shared between all users</param>
        /// <returns>The store, or <see cref="AllApp"/> if the store couldn't be created or uses the same location as <see cref="AllApp"/></returns>
        static KeyValueStore CreateDefault(bool perApp, bool perUser)
        {
            var legacy = AllApp;
            try
            {
                var s = new KeyValueStore(new KeyValueStoreParams
                {
                    PerApp = perApp,
                    PerUser = perUser,
                }, legacy);
                //  Configured to use the same folder(s), no need for a separate store
                if (s.LockPrefix == legacy.LockPrefix)
                    return legacy;
                return s;
            }
            catch
            {
                //  Old behaviour (same instance)
                return legacy;
            }
        }


        static readonly ConcurrentDictionary<String, KeyValueStore> Stores;


        KeyValueStore(KeyValueStoreParams p, KeyValueStore legacy = null)
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
            if (legacy != null)
            {
                Legacy = legacy;
                LegacyDeletedFolder = Path.Combine(folders[0], id, "LegacyDeleted");
            }
        }

        /// <summary>
        /// If non-null, keys that have no valid copy in this store are read from this store (and copied to this store).
        /// </summary>
        readonly KeyValueStore Legacy;

        /// <summary>
        /// Folder with (empty) marker files, one per key that was deleted from this store while the <see cref="Legacy"/> store had a copy (stops the fallback).
        /// </summary>
        readonly String LegacyDeletedFolder;

        /// <summary>
        /// Read a value from the <see cref="Legacy"/> store and copy the data to this store.
        /// Must be called with this store's key lock held.
        /// </summary>
        T TryGetLegacy<T>(String key, T returnWhenNotFound, bool tryAll)
        {
            if (File.Exists(Path.Combine(LegacyDeletedFolder, key)))
                return returnWhenNotFound;
            var legacy = Legacy;
            using var lck = SystemLock.Get(legacy.LockPrefix + key);
            var f = legacy.GetOrderedFiles(key);
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
                T value;
                try
                {
                    value = legacy.Create<T>(data.Memory);
                }
                catch
                {
                    if (!tryAll)
                        throw;
                    continue;
                }
                //  Migrate, the default stores use the same serializer and compression, so the data can be copied as is
                var maxTime = DateTime.UtcNow.AddSeconds(-1);
                foreach (var x in Paths)
                {
                    try
                    {
                        var dest = Path.Combine(x, key);
                        FileExt.WriteMemory(dest, data.Memory, true);
                        //  Keep the time stamp of the legacy copy (at least a second old), so that any later write to this store is newer (copies are ordered by the last write time)
                        File.SetLastWriteTimeUtc(dest, d.Item2.Value < maxTime ? d.Item2.Value : maxTime);
                    }
                    catch
                    {
                    }
                }
                return value;
            }
            return returnWhenNotFound;
        }

        /// <summary>
        /// Read a value from the <see cref="Legacy"/> store and copy the data to this store.
        /// Must be called with this store's key lock held.
        /// </summary>
        async Task<T> TryGetLegacyAsync<T>(String key, T returnWhenNotFound, bool tryAll)
        {
            if (File.Exists(Path.Combine(LegacyDeletedFolder, key)))
                return returnWhenNotFound;
            var legacy = Legacy;
            using var lck = await SystemLock.GetAsync(legacy.LockPrefix + key).ConfigureAwait(false);
            var f = legacy.GetOrderedFiles(key);
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
                T value;
                try
                {
                    value = legacy.Create<T>(data.Memory);
                }
                catch
                {
                    if (!tryAll)
                        throw;
                    continue;
                }
                //  Migrate, the default stores use the same serializer and compression, so the data can be copied as is
                var maxTime = DateTime.UtcNow.AddSeconds(-1);
                foreach (var x in Paths)
                {
                    try
                    {
                        var dest = Path.Combine(x, key);
                        await FileExt.WriteMemoryAsync(dest, data.Memory, true).ConfigureAwait(false);
                        //  Keep the time stamp of the legacy copy (at least a second old), so that any later write to this store is newer (copies are ordered by the last write time)
                        File.SetLastWriteTimeUtc(dest, d.Item2.Value < maxTime ? d.Item2.Value : maxTime);
                    }
                    catch
                    {
                    }
                }
                return value;
            }
            return returnWhenNotFound;
        }

        /// <summary>
        /// If the <see cref="Legacy"/> store has a copy of the key, write a marker so that the deleted key isn't read from the legacy store anymore.
        /// Must be called with this store's key lock held.
        /// </summary>
        void MarkLegacyDeleted(String key)
        {
            var legacy = Legacy;
            if (legacy == null)
                return;
            foreach (var x in legacy.Paths)
            {
                if (!File.Exists(Path.Combine(x, key)))
                    continue;
                var folder = LegacyDeletedFolder;
                PathExt.EnsureFolderExist(folder);
                var name = Path.Combine(folder, key);
                if (!File.Exists(name))
                    File.WriteAllBytes(name, []);
                return;
            }
        }

        /// <summary>
        /// The minimum number of copies that are overwritten on every write (normally <see cref="Redundancy"/> - 1, so the previous value survives a failed write).
        /// </summary>
        public readonly int WriteRedundancy;

        /// <summary>
        /// Make sure that the copies that were just written are newer than the copies that were kept.
        /// The file system time stamp resolution can be coarse, so two writes in quick succession could otherwise get the same last write time (and an old copy could be read).
        /// </summary>
        /// <param name="f">The files ordered from oldest to newest (as before the write)</param>
        /// <param name="writeCount">The number of (oldest) files that were written</param>
        static void EnsureNewest(Tuple<String, DateTime?>[] f, int writeCount)
        {
            var fc = f.Length;
            if (writeCount >= fc)
                return;
            var kept = f[fc - 1].Item2;
            if (kept == null)
                return;
            var min = kept.Value.AddTicks(1);
            for (int i = 0; i < writeCount; ++i)
            {
                var name = f[i].Item1;
                if (File.GetLastWriteTimeUtc(name) < min)
                    File.SetLastWriteTimeUtc(name, min);
            }
        }

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
