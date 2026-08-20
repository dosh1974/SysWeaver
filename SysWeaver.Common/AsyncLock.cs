using System;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver
{
    public sealed class AsyncLock
    {

        /// <summary>
        /// Useful helper when using the coalesce operator ?.
        /// </summary>
        public static readonly Task<IDisposable> NoLock = new Task<IDisposable>(null);


        /// <summary>
        /// Wait for a lock to be taken
        /// </summary>
        /// <returns>An IDisposable that releases the lock</returns>
        public async Task<IDisposable> Lock()
        {
            var d = D;
            await d.S.WaitAsync().ConfigureAwait(false);
            return d;
        }

        /// <summary>
        /// Wait for a lock to be taken, for a limited time
        /// </summary>
        /// <param name="waitMilliSeconds">Number of milliseconds to wait at most</param>
        /// <returns>An IDisposable that releases the lock or null if the wait timed-out and no lock is taken</returns>
        public async Task<IDisposable> Lock(int waitMilliSeconds)
        {
            var d = D;
            if (!await d.S.WaitAsync(waitMilliSeconds).ConfigureAwait(false))
                return null;
            return d;
        }

        /// <summary>
        /// Wait for a lock to be taken
        /// </summary>
        /// <returns>An IDisposable that releases the lock</returns>
        public IDisposable LockSync()
        {
            var d = D;
            d.S.Wait();
            return d;
        }

        /// <summary>
        /// Wait for a lock to be taken, for a limited time
        /// </summary>
        /// <param name="waitMilliSeconds">Number of milliseconds to wait at most</param>
        /// <returns>An IDisposable that releases the lock or null if the wait timed-out and no lock is taken</returns>
        public IDisposable LockSync(int waitMilliSeconds)
        {
            var d = D;
            if (!d.S.Wait(waitMilliSeconds))
                return null;
            return d;
        }

        readonly I D;
        readonly R A;

        /// <summary>
        /// Create a new async lock 
        /// </summary>
        /// <param name="maxConcurrentAccess">Number of allowed concurrent accesses to the loakced resources</param>
        public AsyncLock(int maxConcurrentAccess = 1)
        {
            if (maxConcurrentAccess <= 0)
                maxConcurrentAccess = 1;
            D = new I(maxConcurrentAccess);
            A = new R(maxConcurrentAccess, D.S);
        }

        /// <summary>
        /// Maximum number of threads that can get this lock
        /// </summary>
        public int MaxConcurrentAccess => A.MaxConcurrentAccess;


        /// <summary>
        /// Wait for all locks to be taken
        /// </summary>
        /// <returns>An IDisposable that releases the locks</returns>
        public async Task<IDisposable> LockAll()
        {
            var d = A;
            var c = d.MaxConcurrentAccess;
            var s = d.S;
            for (int i = 0; i < c; ++i)
            {
                try
                {
                    await s.WaitAsync().ConfigureAwait(false);
                }
                catch
                {
                    while (i > 0)
                    {
                        --i;
                        s.Release();
                    }
                    throw;
                }
            }
            return d;
        }

        /// <summary>
        /// Wait for all locks to be taken, for a limited time
        /// </summary>
        /// <param name="waitMilliSeconds">Number of milliseconds to wait at most</param>
        /// <returns>An IDisposable that releases the locks</returns>
        public async Task<IDisposable> LockAll(int waitMilliSeconds)
        {
            var d = A;
            var c = d.MaxConcurrentAccess;
            var s = d.S;
            for (int i = 0; i < c; ++i)
            {
                try
                {
                    if (!await s.WaitAsync(waitMilliSeconds).ConfigureAwait(false))
                    {
                        while (i > 0)
                        {
                            --i;
                            s.Release();
                        }
                        return null;
                    }
                }
                catch
                {
                    while (i > 0)
                    {
                        --i;
                        s.Release();
                    }
                    throw;
                }
            }
            return d;
        }


        /// <summary>
        /// Wait for all locks to be taken
        /// </summary>
        /// <returns>An IDisposable that releases the locks</returns>
        public IDisposable LockAllSync()
        {
            var d = A;
            var c = d.MaxConcurrentAccess;
            var s = d.S;
            for (int i = 0; i < c; ++i)
            {
                try
                {
                    s.Wait();
                }
                catch
                {
                    while (i > 0)
                    {
                        --i;
                        s.Release();
                    }
                    throw;
                }
            }
            return d;
        }

        /// <summary>
        /// Wait for all locks to be taken, for a limited time
        /// </summary>
        /// <param name="waitMilliSeconds">Number of milliseconds to wait at most</param>
        /// <returns>An IDisposable that releases the locks</returns>
        public IDisposable LockAllSync(int waitMilliSeconds)
        {
            var d = A;
            var c = d.MaxConcurrentAccess;
            var s = d.S;
            for (int i = 0; i < c; ++i)
            {
                try
                {
                    if (!s.Wait(waitMilliSeconds))
                    {
                        while (i > 0)
                        {
                            --i;
                            s.Release();
                        }
                        return null;
                    }
                }
                catch
                {
                    while (i > 0)
                    {
                        --i;
                        s.Release();
                    }
                    throw;
                }
            }
            return d;
        }



#if DEBUG
        public override string ToString() => String.Concat("Locked: ", D.S.CurrentCount, '/', A.MaxConcurrentAccess);
#endif//DEBUG


        sealed class I : IDisposable
        {
#if DEBUG
            public override string ToString() => "Lock instance: " + S.CurrentCount;
#endif//DEBUG
            public I(int maxConcurrentAccess)
            {
                S = new SemaphoreSlim(maxConcurrentAccess, maxConcurrentAccess);
            }
            public void Dispose() => S.Release();
            public readonly SemaphoreSlim S;
        }

        sealed class R : IDisposable 
        {
#if DEBUG
            public override string ToString() => String.Concat("Lock all: ", S.CurrentCount, '/', MaxConcurrentAccess);
#endif//DEBUG
            public R(int maxConcurrentAccess, SemaphoreSlim s)
            {
                S = s;
                MaxConcurrentAccess = maxConcurrentAccess;
            }
            public readonly int MaxConcurrentAccess;
            public readonly SemaphoreSlim S;



            public void Dispose()
            {
                var s = S;
                var c = MaxConcurrentAccess;
                while (c > 0)
                {
                    --c;
                    s.Release();
                }
            }
        }


    }


}
