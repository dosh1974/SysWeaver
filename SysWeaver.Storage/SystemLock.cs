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
                    var f = new FileStream(name, FileMode.Create, FileAccess.Write, FileShare.None, 128, FileOptions.DeleteOnClose);
#if DEBUG
                return new Lock(f, "SystemLock: " + key.ToQuoted());
#else//DEBUG
                    return new Lock(f);
#endif//DEBUG
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
                        using var x = new FileStream(name, FileMode.Create, FileAccess.Write, FileShare.None, 128, FileOptions.DeleteOnClose);
                    }
                    return false;
                }
                catch (IOException)
                {
                    if (i >= 3)
                        return true;
                }
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
                var f = new FileStream(name, FileMode.Create, FileAccess.Write, FileShare.None, 128, FileOptions.DeleteOnClose);
#if DEBUG
                lockObject = new Lock(f, "SystemLock: " + key.ToQuoted());
#else//DEBUG
                    lockObject = new Lock(f);
#endif//DEBUG
                return true;
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
                    var f = new FileStream(name, FileMode.Create, FileAccess.Write, FileShare.None, 128, FileOptions.DeleteOnClose);
#if DEBUG
                    return new Lock(f, "SystemLock: " + key.ToQuoted());
#else//DEBUG
                    return new Lock(f);
#endif//DEBUG
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


        /// <summary>
        /// A held lock, closing (and deleting) the exclusively opened lock file releases it.
        /// </summary>
        sealed class Lock : IDisposable
        {

#if DEBUG

            public override string ToString() => S;
            readonly String S;

            public Lock(FileStream m, String s)
            {
                M = m;
                S = s;
            }

#else//DEBUG

            public Lock(FileStream m)
            {
                M = m;
            }

#endif//DEBUG


            FileStream M;


            void TryDispose()
            {
                for (int i = 0; ; ++i)
                {
                    try
                    {
                        Interlocked.Exchange(ref M, null)?.Dispose();
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
