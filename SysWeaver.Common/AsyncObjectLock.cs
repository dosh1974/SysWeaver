using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver
{

    /// <summary>
    /// Lock access per object (key), only one holder can have the lock for a given key at a time, different keys are independent.
    /// </summary>
    /// <typeparam name="T">The type of the objects (keys) to lock on</typeparam>
    /// <remarks>
    /// The lock is not re-entrant, locking the same key again while holding it will wait forever.
    /// Waiters are not queued, they poll for the lock (spinning shortly and then checking every millisecond or so), so the lock is not fair and is intended for low contention scenarios.
    /// </remarks>
    public sealed class AsyncObjectLock<T>
    {
        readonly LowAllocConcurrentDictionary<T, long> Locks;

        /// <summary>
        /// Used to create a unique value for every lock taken, so that a handle can only release the lock that it took
        /// </summary>
        long Counter;

        /// <summary>
        /// Create a new object lock
        /// </summary>
        /// <param name="comparer">The comparer to use for the objects (keys), null to use the default equality comparer</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AsyncObjectLock(IEqualityComparer<T> comparer = null)
        {
            Locks = new LowAllocConcurrentDictionary<T, long>(comparer ?? EqualityComparer<T>.Default);
        }

        /// <summary>
        /// Release a lock (only if it's still owned by the handle)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Free(T obj, long token)
            => Locks.Remove(new KeyValuePair<T, long>(obj, token));

        /// <summary>
        /// Wait for the lock of an object to be taken
        /// </summary>
        /// <param name="obj">The object (key) to lock on</param>
        /// <returns>A handle that must be disposed to release the lock</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is null</exception>
        /// <remarks>If the lock is free, the returned value task is already completed and no memory is allocated</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<AsyncObjectLockHandle<T>> LockAsync(T obj)
        {
            var token = Interlocked.Increment(ref Counter);
            return Locks.TryAdd(obj, token) ? ValueTask.FromResult(new AsyncObjectLockHandle<T>(obj, this, token)) : InternalLockAsync(obj, token);
        }

        async ValueTask<AsyncObjectLockHandle<T>> InternalLockAsync(T obj, long token)
        {
            var l = Locks;
            await Task.Yield();
            if (l.TryAdd(obj, token))
                return new AsyncObjectLockHandle<T>(obj, this, token);
            var sw = new SpinWait();
            for (int i = 0; i < 32; ++ i)
            {
                sw.SpinOnce();
                if (l.TryAdd(obj, token))
                    return new AsyncObjectLockHandle<T>(obj, this, token);
            }
            while (true)
            {
                await Task.Delay(1).ConfigureAwait(false);
                if (l.TryAdd(obj, token))
                    return new AsyncObjectLockHandle<T>(obj, this, token);
            }
        }


        /// <summary>
        /// Try to take the lock of an object, without waiting
        /// </summary>
        /// <param name="obj">The object (key) to lock on</param>
        /// <param name="handle">If successful, a handle that must be disposed to release the lock, else a default handle (that does nothing when disposed)</param>
        /// <returns>True if the lock was taken</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is null</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryLock(T obj, out AsyncObjectLockHandle<T> handle)
        {
            var token = Interlocked.Increment(ref Counter);
            var r = Locks.TryAdd(obj, token);
            handle = r ? new AsyncObjectLockHandle<T>(obj, this, token) : default;
            return r;
        }


    }

    /// <summary>
    /// A handle to a lock taken by <see cref="AsyncObjectLock{T}"/>, dispose it to release the lock.
    /// </summary>
    /// <typeparam name="T">The type of the objects (keys) to lock on</typeparam>
    /// <remarks>
    /// Disposing a handle more than once (or disposing a copy of it) is safe, only the first dispose releases the lock (a lock taken later by someone else is never released).
    /// Disposing a default handle does nothing.
    /// </remarks>
    public readonly struct AsyncObjectLockHandle<T> : IDisposable
    {
#if DEBUG
        public override string ToString() => "Lock for " + V;
#endif//DEBUG

        /// <summary>
        /// Create an empty handle (disposing it does nothing)
        /// </summary>
        public AsyncObjectLockHandle()
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal AsyncObjectLockHandle(T t, AsyncObjectLock<T> owner, long token)
        {
            V = t;
            Owner = owner;
            Token = token;
        }

        readonly T V;
        readonly AsyncObjectLock<T> Owner;
        readonly long Token;

        /// <summary>
        /// Release the lock (if this handle still owns it)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
            => Owner?.Free(V, Token);


    }



}
