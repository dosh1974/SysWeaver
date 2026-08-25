using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver
{

    /// <summary>
    /// Lock access for one per 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class AsyncObjectLock<T>
    {
        readonly LowAllocConcurrentDictionary<T, int> Locks;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AsyncObjectLock(IEqualityComparer<T> comparer = null)
        {
            var l = new LowAllocConcurrentDictionary<T, int>(comparer ?? EqualityComparer<T>.Default);
            Locks = l;
            Free = x => l.TryRemove(x, out _);
        }

        readonly Action<T> Free;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<AsyncObjectLockHandle<T>> LockAsync(T obj)
            => Locks.TryAdd(obj, 0) ? ValueTask.FromResult(new AsyncObjectLockHandle<T>(obj, Free)) : InternalLockAsync(obj);

        async ValueTask<AsyncObjectLockHandle<T>> InternalLockAsync(T obj)
        {
            var l = Locks;
            await Task.Yield();
            if (l.TryAdd(obj, 0))
                return new AsyncObjectLockHandle<T>(obj, Free);
            var sw = new SpinWait();
            for (int i = 0; i < 100; ++ i)
            {
                sw.SpinOnce();
                if (l.TryAdd(obj, 0))
                    return new AsyncObjectLockHandle<T>(obj, Free);
            }
            while (true)
            {
                await Task.Delay(1).ConfigureAwait(false);
                if (l.TryAdd(obj, 0))
                    return new AsyncObjectLockHandle<T>(obj, Free);
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryLock(T obj, out AsyncObjectLockHandle<T> handle)
        {
            var r = Locks.TryAdd(obj, 0);
            handle = r ? new AsyncObjectLockHandle<T>(obj, Free) : default;
            return r;
        }


    }

    public readonly struct AsyncObjectLockHandle<T> : IDisposable
    {
        public AsyncObjectLockHandle()
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal AsyncObjectLockHandle(T t, Action<T> free)
        {
            V = t;
            Free = free;
        }

        readonly T V;
        readonly Action<T> Free;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
            => Free(V);


    }



}
