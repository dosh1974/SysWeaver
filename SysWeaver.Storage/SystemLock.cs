using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver
{

    /// <summary>
    /// System wide locks (using temporary files so available on OS'es)
    /// </summary>
    public static class SystemLock
    {

        /// <summary>
        /// Get the lock (or wait forever until it's available)
        /// </summary>
        /// <param name="key">The key to lock on (MD5 checksum of the string is what's actually being used to allow for any text here)</param>
        /// <returns>A lock object, dispose to unlock</returns>
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
        /// Try to get the lock
        /// </summary>
        /// <param name="key">The key to lock on (MD5 checksum of the string is what's actually being used to allow for any text here)</param>
        /// <param name="lockObject">If successful, a lock object, dispose to unlock</param>
        /// <returns>True of the lock was successful else false</returns>
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
        /// Get the lock (or wait forever until it's available)
        /// </summary>
        /// <param name="key">The key to lock on (MD5 checksum of the string is what's actually being used to allow for any text here)</param>
        /// <returns>A lock object, dispose to unlock</returns>
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
