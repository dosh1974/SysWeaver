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
        public static readonly ValueTask<IDisposable> NoLock = new ValueTask<IDisposable>(null);


        /// <summary>
        /// Wait for a lock to be taken
        /// </summary>
        /// <returns>An IDisposable that releases the lock</returns>
        public async ValueTask<IDisposable> Lock()
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
        public async ValueTask<IDisposable> Lock(int waitMilliSeconds)
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

        /// <summary>
        /// Create a new async lock 
        /// </summary>
        /// <param name="maxConcurrentAccess">Number of allowed concurrent accesses to the loakced resources</param>
        public AsyncLock(int maxConcurrentAccess = 1)
        {
            D = new I(maxConcurrentAccess);
        }

        sealed class I : IDisposable
        {
            public I(int maxConcurrentAccess)
            {
                if (maxConcurrentAccess <= 0)
                    maxConcurrentAccess = 1;
                S = new SemaphoreSlim(maxConcurrentAccess, maxConcurrentAccess);
            }
            public void Dispose() => S.Release();
            public readonly SemaphoreSlim S;
        }
    }


}
