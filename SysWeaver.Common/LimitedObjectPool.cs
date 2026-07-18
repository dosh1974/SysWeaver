using System;
using System.Collections.Concurrent;
using System.Threading;

namespace SysWeaver
{
    /// <summary>
    /// An concurrent object pool with a limited number of cached objects.
    /// There is a small chance that the number of objects exceeds the maximum.
    /// The object must implement IDisposable and call the supplied Action there.
    /// Do not perform any acutal disposing of required internal resources, resetting the object for re-use is fine.
    /// If disposing of required internal resources is required, supply a disposer that will be called when an instance in no longer needed.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class LimitedObjectPool<T> : IDisposable where T : IDisposable
    {
        public LimitedObjectPool(Func<Action<T>, T> creator, int maxCached, Action<T> disposer = null)
        {
            maxCached = Math.Max(1, maxCached);
            MaxCached = maxCached;
            InternalMaxCached = maxCached;
            Creator = creator;
            Disposer = disposer;
            OnDispose = OnDisposeFn;
        }

        /// <summary>
        /// Get a cached object or create a new one, use the using pattern (calling Dispose when the object can be re-used)
        /// </summary>
        /// <returns>An instance</returns>
        public T Get()
        {
            Interlocked.Increment(ref InternalInUse);
            if (Cached.TryPop(out var t))
            {
                Interlocked.Decrement(ref InternalInCache);
                return t;
            }
            t = Creator(OnDispose);
            Interlocked.Increment(ref InternalCreated);
            return t;
        }

        /// <summary>
        /// This will remove all objects from the cache and call there disposer function.
        /// Any subsequent calls to Get will return a new instance.
        /// Any Dispose calls to objects not in the cache will call the disposer functions directly.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref InternalMaxCached, 0) == 0)
                return;
            var od = Disposer;
            var cache = Cached;
            if (od != null)
            {
                do
                {
                    while (cache.TryPop(out var p))
                    {
                        Interlocked.Decrement(ref InternalInCache);
                        Interlocked.Increment(ref InternalDisposed);
                        try
                        {
                            od.Invoke(p);
                        }
                        catch
                        {
                        }
                    }
                    // There is a small chance that an object was disposed before after the Interlocked.Exchange happened.
                    // But the disposed object was pushed to the cache after this clearing of the cache.
                    // Sleep a bit and re-try, this makes the Dispose slower than it need but it's better to play it safe than having undisposed resources
                    Thread.Sleep(5);
                } while (cache.TryPeek(out var _));
            }else
            {
                while (cache.TryPop(out var p))
                {
                    Interlocked.Decrement(ref InternalInCache);
                    Interlocked.Increment(ref InternalDisposed);
                }
                // There is a small chance that an object was disposed before after the Interlocked.Exchange happened.
                // But the disposed object was pushed to the cache after this clearing of the cache.
                // Since we don't have a dispose we simply keep a reference to these objects (possible prevent GC'ing if there is a reference to LimitedObjectPool instance after it's disposal).
            }
        }

        /// <summary>
        /// The maximum number of instances to cache (the cache can exceed this number by some amount under heavy concurrent load)
        /// </summary>
        public readonly int MaxCached;

        /// <summary>
        /// Number of instances currently in the cache
        /// </summary>
        public long InCache => Interlocked.Read(ref InternalInCache);
        
        /// <summary>
        /// Number of instance in use (Get called, but not Disposed)
        /// </summary>
        public long InUse => Interlocked.Read(ref InternalInUse);
        
        /// <summary>
        /// Total number of created instances
        /// </summary>
        public long Created => Interlocked.Read(ref InternalCreated);

        /// <summary>
        /// Total number of disposed instances
        /// </summary>
        public long Disposed => Interlocked.Read(ref InternalDisposed);

        void OnDisposeFn(T t)
        {
            Interlocked.Decrement(ref InternalInUse);
            if (Interlocked.Read(ref InternalInCache) >= InternalMaxCached)
            {
                Interlocked.Increment(ref InternalDisposed);
                Disposer?.Invoke(t);
                return;
            }
            Interlocked.Increment(ref InternalInCache);
            Cached.Push(t);
        }

        readonly Action<T> OnDispose;
        volatile int InternalMaxCached;

        long InternalInCache;
        long InternalInUse;
        long InternalCreated;
        long InternalDisposed;
        readonly ConcurrentStack<T> Cached = new ();
        readonly Func<Action<T>, T> Creator;
        readonly Action<T> Disposer;
    }
}
