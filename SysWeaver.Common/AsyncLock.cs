using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver
{
    /// <summary>
    /// An async lock (a semaphore) that can be held by up to <see cref="MaxConcurrentAccess"/> holders at the same time.
    /// The lock can be taken async (Lock), synchronously (LockSync) or without waiting (TryLock).
    /// All the slots can be taken at once (LockAll, LockAllSync, TryLockAll), giving exclusive access.
    /// </summary>
    /// <remarks>
    /// The lock is not re-entrant, taking the lock again while holding it (with a max concurrency of one) will dead lock.
    /// The returned IDisposable releases the lock, it must be disposed exactly once (the same instance is returned to all holders, so disposing it twice will release a slot owned by someone else).
    /// </remarks>
    public sealed class AsyncLock
    {

        /// <summary>
        /// Useful helper when using the coalesce operator ?.
        /// A completed task with a null result (a null IDisposable is ignored by the using statement).
        /// </summary>
        public static readonly Task<IDisposable> NoLock = Task.FromResult<IDisposable>(null);


        /// <summary>
        /// Wait for a lock to be taken
        /// </summary>
        /// <returns>An IDisposable that releases the lock</returns>
        /// <remarks>If the lock is free, the returned task is already completed and no memory is allocated</remarks>
        public Task<IDisposable> Lock()
        {
            var d = D;
            var t = d.S.WaitAsync();
            return t.IsCompletedSuccessfully ? d.Locked : WaitLock(t, d);
        }

        static async Task<IDisposable> WaitLock(Task t, I d)
        {
            await t.ConfigureAwait(false);
            return d;
        }

        static async Task<IDisposable> WaitLock(Task<bool> t, I d)
            => (await t.ConfigureAwait(false)) ? d : null;


        /// <summary>
        /// Try to get a lock, returns null if a lock can't be obtained
        /// </summary>
        /// <returns>An IDisposable that releases the lock, or null if the lock is taken</returns>
        public IDisposable TryLock()
        {
            var d = D;
            return d.S.Wait(0) ? d : null;
        }

        /// <summary>
        /// Try to lock all, returns null if all locks can't be obtained
        /// </summary>
        /// <returns>An IDisposable that releases the locks, or null if any slot is taken</returns>
        public IDisposable TryLockAll()
        {
            var d = A;
            var c = d.MaxConcurrentAccess;
            var s = d.S;
            for (int i = 0; i < c; ++i)
            {
                try
                {
                    if (!s.Wait(0))
                    {
                        Release(s, i);
                        return null;
                    }
                }
                catch
                {
                    Release(s, i);
                    throw;
                }
            }
            return d;
        }


        /// <summary>
        /// Wait for a lock to be taken, for a limited time
        /// </summary>
        /// <param name="waitMilliSeconds">Number of milliseconds to wait at most, 0 to not wait at all or -1 (<see cref="Timeout.Infinite"/>) to wait forever</param>
        /// <returns>An IDisposable that releases the lock or null if the wait timed-out and no lock is taken</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="waitMilliSeconds"/> is less than -1 and the caller must wait (the returned task is faulted)</exception>
        /// <remarks>If the lock is free, the returned task is already completed and no memory is allocated</remarks>
        public Task<IDisposable> Lock(int waitMilliSeconds)
        {
            var d = D;
            var t = WaitAsync(d.S, waitMilliSeconds);
            if (t.IsCompletedSuccessfully)
                return t.Result ? d.Locked : NoLock;
            return WaitLock(t, d);
        }

        static readonly Task<bool> True = Task.FromResult(true);

        /// <summary>
        /// SemaphoreSlim.WaitAsync, but with a timeout less than -1 it don't register a waiter (that would take a slot that is never released) when returning the faulted task
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Task<bool> WaitAsync(SemaphoreSlim s, int waitMilliSeconds)
        {
            if (waitMilliSeconds >= -1)
                return s.WaitAsync(waitMilliSeconds);
            return InvalidWaitAsync(s, waitMilliSeconds);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static Task<bool> InvalidWaitAsync(SemaphoreSlim s, int waitMilliSeconds)
        {
            // Same behaviour as SemaphoreSlim: succeeds if a slot is free, else a faulted task
            if (s.Wait(0))
                return True;
            return Task.FromException<bool>(new ArgumentOutOfRangeException(nameof(waitMilliSeconds), waitMilliSeconds, "The timeout must be -1 (infinite) or greater"));
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
        /// <param name="waitMilliSeconds">Number of milliseconds to wait at most, 0 to not wait at all or -1 (<see cref="Timeout.Infinite"/>) to wait forever</param>
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
        /// Serializes the LockAll callers, without it two concurrent LockAll calls could each get some of the slots and then wait forever for each other.
        /// Created on first use (only needed if MaxConcurrentAccess is greater than one).
        /// </summary>
        SemaphoreSlim G;

        SemaphoreSlim AllGate
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var g = G;
                if (g != null)
                    return g;
                g = new SemaphoreSlim(1, 1);
                return Interlocked.CompareExchange(ref G, g, null) ?? g;
            }
        }

        /// <summary>
        /// Create a new async lock
        /// </summary>
        /// <param name="maxConcurrentAccess">Number of allowed concurrent accesses to the locked resources, values less than one are treated as one</param>
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Release(SemaphoreSlim s, int count)
        {
            if (count > 0)
                s.Release(count);
        }

        /// <summary>
        /// Get the time remaining until a timeout
        /// </summary>
        /// <param name="waitMilliSeconds">The original timeout</param>
        /// <param name="start">The Environment.TickCount64 when the wait started</param>
        /// <returns>The number of milliseconds left to wait (-1 = infinite)</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int Remaining(int waitMilliSeconds, long start)
        {
            if (waitMilliSeconds <= 0)
                return waitMilliSeconds;
            var left = waitMilliSeconds - (Environment.TickCount64 - start);
            return left <= 0 ? 0 : (int)left;
        }


        /// <summary>
        /// Wait for all locks to be taken
        /// </summary>
        /// <returns>An IDisposable that releases the locks</returns>
        /// <remarks>Concurrent LockAll callers are served one at a time (they can't dead lock each other)</remarks>
        public async Task<IDisposable> LockAll()
        {
            var d = A;
            var c = d.MaxConcurrentAccess;
            var s = d.S;
            if (c == 1)
            {
                await s.WaitAsync().ConfigureAwait(false);
                return d;
            }
            var g = AllGate;
            await g.WaitAsync().ConfigureAwait(false);
            try
            {
                for (int i = 0; i < c; ++i)
                {
                    try
                    {
                        await s.WaitAsync().ConfigureAwait(false);
                    }
                    catch
                    {
                        Release(s, i);
                        throw;
                    }
                }
            }
            finally
            {
                g.Release();
            }
            return d;
        }

        /// <summary>
        /// Wait for all locks to be taken, for a limited time
        /// </summary>
        /// <param name="waitMilliSeconds">Number of milliseconds to wait at most (in total), 0 to not wait at all or -1 (<see cref="Timeout.Infinite"/>) to wait forever</param>
        /// <returns>An IDisposable that releases the locks or null if the wait timed-out and no lock is taken</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="waitMilliSeconds"/> is less than -1 and the caller must wait (the returned task is faulted)</exception>
        /// <remarks>Concurrent LockAll callers are served one at a time (they can't dead lock each other)</remarks>
        public async Task<IDisposable> LockAll(int waitMilliSeconds)
        {
            var d = A;
            var c = d.MaxConcurrentAccess;
            var s = d.S;
            if (c == 1)
                return (await WaitAsync(s, waitMilliSeconds).ConfigureAwait(false)) ? d : null;
            var start = Environment.TickCount64;
            var g = AllGate;
            if (!await WaitAsync(g, waitMilliSeconds).ConfigureAwait(false))
                return null;
            try
            {
                for (int i = 0; i < c; ++i)
                {
                    try
                    {
                        if (!await WaitAsync(s, Remaining(waitMilliSeconds, start)).ConfigureAwait(false))
                        {
                            Release(s, i);
                            return null;
                        }
                    }
                    catch
                    {
                        Release(s, i);
                        throw;
                    }
                }
            }
            finally
            {
                g.Release();
            }
            return d;
        }


        /// <summary>
        /// Wait for all locks to be taken
        /// </summary>
        /// <returns>An IDisposable that releases the locks</returns>
        /// <remarks>Concurrent LockAll callers are served one at a time (they can't dead lock each other)</remarks>
        public IDisposable LockAllSync()
        {
            var d = A;
            var c = d.MaxConcurrentAccess;
            var s = d.S;
            if (c == 1)
            {
                s.Wait();
                return d;
            }
            var g = AllGate;
            g.Wait();
            try
            {
                for (int i = 0; i < c; ++i)
                {
                    try
                    {
                        s.Wait();
                    }
                    catch
                    {
                        Release(s, i);
                        throw;
                    }
                }
            }
            finally
            {
                g.Release();
            }
            return d;
        }

        /// <summary>
        /// Wait for all locks to be taken, for a limited time
        /// </summary>
        /// <param name="waitMilliSeconds">Number of milliseconds to wait at most (in total), 0 to not wait at all or -1 (<see cref="Timeout.Infinite"/>) to wait forever</param>
        /// <returns>An IDisposable that releases the locks or null if the wait timed-out and no lock is taken</returns>
        /// <remarks>Concurrent LockAll callers are served one at a time (they can't dead lock each other)</remarks>
        public IDisposable LockAllSync(int waitMilliSeconds)
        {
            var d = A;
            var c = d.MaxConcurrentAccess;
            var s = d.S;
            if (c == 1)
                return s.Wait(waitMilliSeconds) ? d : null;
            var start = Environment.TickCount64;
            var g = AllGate;
            if (!g.Wait(waitMilliSeconds))
                return null;
            try
            {
                for (int i = 0; i < c; ++i)
                {
                    try
                    {
                        if (!s.Wait(Remaining(waitMilliSeconds, start)))
                        {
                            Release(s, i);
                            return null;
                        }
                    }
                    catch
                    {
                        Release(s, i);
                        throw;
                    }
                }
            }
            finally
            {
                g.Release();
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

            Task<IDisposable> L;

            /// <summary>
            /// A completed task with this instance as the result (created on first use)
            /// </summary>
            public Task<IDisposable> Locked
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => L ?? (L = Task.FromResult<IDisposable>(this));
            }
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

            public void Dispose() => S.Release(MaxConcurrentAccess);
        }


    }



}
