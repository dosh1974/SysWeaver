using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver
{

    /// <summary>
    /// System wide (cross process) named locks, implemented using exclusively opened lock files (delete on close) so they work on all OS'es.
    /// </summary>
    /// <remarks>
    /// The lock file name is an MD5 hash of the key, stored in the "SystemLock" <see cref="TempFolder"/>.
    /// Locks are NOT re-entrant, taking the same lock twice from the same thread / async flow will dead lock.
    /// Waiting is done by polling every 10 ms (no fairness).
    /// A lock object that isn't disposed is released by its finalizer.
    /// On Unix the exclusivity relies on advisory file locking, so all participants must use this class.
    /// On Unix a lock file is only considered taken if the opened file is still the one linked at the path (a waiter may open the old file just before
    /// the previous holder unlinks it), and the holder unlinks the path before closing the file (it's not opened with delete on close).
    /// </remarks>
    public static class SystemLock
    {

        /// <summary>
        /// Get the lock (or wait forever until it's available), blocking the calling thread.
        /// </summary>
        /// <param name="key">The key to lock on (MD5 checksum of the string is what's actually being used to allow for any text here)</param>
        /// <returns>A lock object, dispose to unlock</returns>
        /// <exception cref="Exception">Opening the lock file failed with a non IO exception (ex: access denied) more than 10 times.</exception>
        public static IDisposable Get(String key)
        {
            var name = GetFilename(key);
            for (int errCount = 0; ; )
            {
                try
                {
                    var l = TryOpen(name, key);
                    if (l != null)
                        return l;
                    Thread.Sleep(10);
                }
                catch (IOException)
                {
                    Thread.Sleep(10);
                }
                catch
                {
                    if (errCount >= 10)
                        throw;
                    ++errCount;
                    Thread.Sleep(10);
                }
            }
        }

        /// <summary>
        /// Check if a lock is currently taken (by briefly trying to take it).
        /// The result may be outdated as soon as it's returned.
        /// </summary>
        /// <param name="key">The key to lock on (MD5 checksum of the string is what's actually being used to allow for any text here)</param>
        /// <returns>True if the lock is taken else false</returns>
        public static bool IsLocked(String key)
        {
            var name = GetFilename(key);
            for (int i = 1; ; ++i)
            {
                try
                {
                    lock (CheckLock)
                    {
                        using var x = TryOpen(name, key);
                        if (x != null)
                            return false;
                    }
                }
                catch (IOException)
                {
                }
                if (i >= 3)
                    return true;
            }
        }

        static readonly Object CheckLock = new ();

        /// <summary>
        /// Try to get the lock without waiting.
        /// </summary>
        /// <param name="key">The key to lock on (MD5 checksum of the string is what's actually being used to allow for any text here)</param>
        /// <param name="lockObject">If successful, a lock object, dispose to unlock</param>
        /// <returns>True if the lock was taken else false</returns>
        public static bool TryGet(String key, out IDisposable lockObject)
        {
            var name = GetFilename(key);
            try
            {
                var l = TryOpen(name, key);
                lockObject = l;
                return l != null;
            }
            catch (IOException)
            {
                lockObject = null;
                return false;
            }
        }

        /// <summary>
        /// Get the lock (or wait forever until it's available), waiting asynchronously.
        /// </summary>
        /// <param name="key">The key to lock on (MD5 checksum of the string is what's actually being used to allow for any text here)</param>
        /// <returns>A lock object, dispose to unlock</returns>
        /// <exception cref="Exception">Opening the lock file failed with a non IO exception (ex: access denied) more than 10 times.</exception>
        public static async Task<IDisposable> GetAsync(String key)
        {
            var name = GetFilename(key);
            for (int errCount = 0; ; )
            {
                try
                {
                    var l = TryOpen(name, key);
                    if (l != null)
                        return l;
                    await Task.Delay(10).ConfigureAwait(false);
                }
                catch (IOException)
                {
                    await Task.Delay(10).ConfigureAwait(false);
                }
                catch
                {
                    if (errCount >= 10)
                        throw;
                    ++errCount;
                    Thread.Sleep(10);
                }

            }
        }


        static readonly String Folder = TempFolder.Get("SystemLock", 5);

        static String GetFilename(String key) => Path.Combine(Folder, HashTools.GetHashString(key));

        static readonly bool IsWindows = OperatingSystem.IsWindows();

        /// <summary>
        /// Try to take the lock file.
        /// </summary>
        /// <param name="name">The lock file name</param>
        /// <param name="key">The key (for debugging)</param>
        /// <returns>The lock, or null if the opened file turned out to be stale (Unix only, retry)</returns>
        /// <exception cref="IOException">The lock is taken</exception>
        static Lock TryOpen(String name, String key)
        {
            if (IsWindows)
            {
                var wf = new FileStream(name, FileMode.Create, FileAccess.Write, FileShare.None, 128, FileOptions.DeleteOnClose);
#if DEBUG
                return new Lock(wf, null, "SystemLock: " + key.ToQuoted());
#else//DEBUG
                return new Lock(wf, null);
#endif//DEBUG
            }
            //  Unix: FileShare.None is an advisory flock on the inode, and delete on close unlinks the path before the lock is released,
            //  so a waiter can lock an inode that is no longer linked at the path (while someone else creates and locks a new file).
            //  Make sure that the locked file is the one linked at the path, the holder unlinks the path (while still holding the lock) on release.
            var f = new FileStream(name, FileMode.Create, FileAccess.Write, FileShare.None, 128, FileOptions.None);
            bool linked;
            try
            {
                linked = IsLinked(f, name);
            }
            catch
            {
                f.Dispose();
                throw;
            }
            if (!linked)
            {
                //  Stale file (unlinked by the previous holder), just close it (don't touch the path, it belongs to someone else)
                f.Dispose();
                return null;
            }
#if DEBUG
            return new Lock(f, name, "SystemLock: " + key.ToQuoted());
#else//DEBUG
            return new Lock(f, name);
#endif//DEBUG
        }

        /// <summary>
        /// Check if the (locked) file is the file currently linked at the path.
        /// Sets a random last write time on the open handle and compares it with the last write time of the path (stat, doesn't open the file).
        /// </summary>
        static bool IsLinked(FileStream f, String name)
        {
            var h = f.SafeFileHandle;
            try
            {
                File.SetLastWriteTimeUtc(h, RandomTime());
            }
            catch
            {
            }
            var handleTime = File.GetLastWriteTimeUtc(h);
            var fi = new FileInfo(name);
            if (!fi.Exists)
                return false;
            return fi.LastWriteTimeUtc == handleTime;
        }

        static readonly DateTime RandomTimeBase = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        static DateTime RandomTime() => RandomTimeBase.AddSeconds(Random.Shared.Next(1 << 30));


        /// <summary>
        /// A held lock, closing (and deleting) the exclusively opened lock file releases it.
        /// On Unix the path is unlinked (while the lock is still held) before closing the file.
        /// </summary>
        sealed class Lock : IDisposable
        {

#if DEBUG

            public override string ToString() => S;
            readonly String S;

            public Lock(FileStream m, String unlinkName, String s)
            {
                M = m;
                UnlinkName = unlinkName;
                S = s;
            }

#else//DEBUG

            public Lock(FileStream m, String unlinkName)
            {
                M = m;
                UnlinkName = unlinkName;
            }

#endif//DEBUG


            FileStream M;

            /// <summary>
            /// Unix only: the path to unlink before closing the file (null on Windows where delete on close is used)
            /// </summary>
            readonly String UnlinkName;


            void TryDispose()
            {
                var m = Interlocked.Exchange(ref M, null);
                if (m == null)
                    return;
                var n = UnlinkName;
                if (n != null)
                {
                    try
                    {
                        File.Delete(n);
                    }
                    catch
                    {
                    }
                }
                for (int i = 0; ; ++i)
                {
                    try
                    {
                        m.Dispose();
                        return;
                    }
                    catch
                    {
                    }
                    if (i >= 10)
                        return;
                    Thread.Sleep(i * 100 + 10);
                }
            }

            public void Dispose()
            {
                TryDispose();
                GC.SuppressFinalize(this);
            }

            ~Lock()
            {
                TryDispose();
            }

        }




    }


}
