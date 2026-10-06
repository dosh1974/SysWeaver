using System;
using System.Collections.Concurrent;
using System.Threading;

namespace SysWeaver
{
    /// <summary>
    /// A concurrent object pool with a limited number of cached objects.
    /// There is a small chance that the number of objects exceeds the maximum.
    /// The object must implement IDisposable and call the supplied Action there.
    /// Do not perform any actual disposing of required internal resources, resetting the object for re-use is fine.
    /// If disposing of required internal resources is required, supply a disposer that will be called when an instance in no longer needed.
    /// </summary>
    /// <typeparam name="T">The type of the pooled objects</typeparam>
    /// <remarks>
    /// The pool is lock free, cached objects are re-used in LIFO order (the most recently returned object is returned first).
    /// Objects must only be returned (disposed) once per <see cref="Get"/>.
    /// </remarks>
    public sealed class LimitedObjectPool<T> : IDisposable where T : IDisposable
    {
        /// <summary>
        /// Create a new pool
        /// </summary>
        /// <param name="creator">A function that creates a new object, the supplied action must be called by the object's Dispose method (this returns the object to the pool)</param>
        /// <param name="maxCached">The maximum number of objects to keep in the cache, values less than one are treated as one</param>
        /// <param name="disposer">An optional action that disposes the internal resources of an object, called when an object is no longer needed (not cached), exceptions thrown by it are ignored when the pool is disposed</param>
        /// <exception cref="ArgumentNullException"><paramref name="creator"/> is null</exception>
        public LimitedObjectPool(Func<Action<T>, T> creator, int maxCached, Action<T> disposer = null)
        {
            ArgumentNullException.ThrowIfNull(creator);
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
        /// <remarks>Any exception thrown by the creator function is propagated</remarks>
        public T Get()
        {
            Interlocked.Increment(ref InternalInUse);
            if (Cached.TryPop(out var t))
            {
                Interlocked.Decrement(ref InternalInCache);
                return t;
            }
            try
            {
                t = Creator(OnDispose);
            }
            catch
            {
                // No instance is in use
                Interlocked.Decrement(ref InternalInUse);
                throw;
            }
            Interlocked.Increment(ref InternalCreated);
            return t;
        }

        /// <summary>
        /// This will remove all objects from the cache and call their disposer function.
        /// Any subsequent calls to Get will return a new instance.
        /// Any Dispose calls to objects not in the cache will call the disposer functions directly.
        /// Calling Dispose more than once does nothing.
        /// </summary>
        /// <remarks>If a disposer function is supplied, this method may sleep a few milliseconds to make sure that objects returned concurrently are disposed too</remarks>
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
                    // An object returned concurrently (pushed after this clearing of the cache) is disposed by the returning thread itself (see OnDisposeFn).
                    // Sleep a bit and re-try anyway, this makes the Dispose slower than it need but it's better to play it safe than having undisposed resources
                    Thread.Sleep(5);
                } while (cache.TryPeek(out var _));
            }else
            {
                while (cache.TryPop(out var p))
                {
                    Interlocked.Decrement(ref InternalInCache);
                    Interlocked.Increment(ref InternalDisposed);
                }
                // An object returned concurrently (pushed after this clearing of the cache) is popped by the returning thread itself (see OnDisposeFn).
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
            var cache = Cached;
            cache.Push(t);
            // If the pool was disposed concurrently, Dispose may already have drained the cache (before our push), so drain it here (else the object would never be disposed)
            if (InternalMaxCached != 0)
                return;
            var od = Disposer;
            while (cache.TryPop(out var p))
            {
                Interlocked.Decrement(ref InternalInCache);
                Interlocked.Increment(ref InternalDisposed);
                if (od == null)
                    continue;
                try
                {
                    od.Invoke(p);
                }
                catch
                {
                }
            }
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
