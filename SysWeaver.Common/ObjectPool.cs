using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace SysWeaver
{
    /// <summary>
    /// Creates object pools
    /// </summary>
    /// <remarks>
    /// The pools are unbounded, all returned objects are kept until the pool is disposed.
    /// Use <see cref="LimitedObjectPool{T}"/> to limit the number of cached objects.
    /// </remarks>
    public static class ObjectPool
    {
        /// <summary>
        /// Create a pool of objects that are created using the parameterless constructor
        /// </summary>
        /// <typeparam name="T">The type of the pooled objects</typeparam>
        /// <returns>A new empty pool</returns>
        public static ObjectPool<T> Create<T>() where T : new() => new ObjectPool<T>(() => new T());

        /// <summary>
        /// Create a pool of objects that are created using the supplied function
        /// </summary>
        /// <typeparam name="T">The type of the pooled objects</typeparam>
        /// <param name="creator">The function that creates a new object (called when the pool is empty)</param>
        /// <returns>A new empty pool</returns>
        /// <exception cref="ArgumentNullException"><paramref name="creator"/> is null</exception>
        public static ObjectPool<T> Create<T>(Func<T> creator)
        {
            ArgumentNullException.ThrowIfNull(creator);
            return new ObjectPool<T>(creator);
        }

        /// <summary>
        /// Create a pool of objects that are created async using the supplied function
        /// </summary>
        /// <typeparam name="T">The type of the pooled objects</typeparam>
        /// <param name="creator">The function that creates a new object (called when the pool is empty)</param>
        /// <returns>A new empty pool</returns>
        /// <exception cref="ArgumentNullException"><paramref name="creator"/> is null</exception>
        public static AsyncObjectPool<T> CreateAsync<T>(Func<Task<T>> creator)
        {
            ArgumentNullException.ThrowIfNull(creator);
            return new AsyncObjectPool<T>(creator);
        }

    }

    /// <summary>
    /// An object allocated from an <see cref="ObjectPool{T}"/> or an <see cref="AsyncObjectPool{T}"/>.
    /// Dispose it to return the object to the pool, it's implicitly converted to the object.
    /// </summary>
    /// <typeparam name="T">The type of the pooled object</typeparam>
    /// <remarks>
    /// Dispose must be called exactly once, disposing it twice will return the object to the pool twice (and two users will get the same object).
    /// Disposing a default instance will throw a <see cref="NullReferenceException"/>.
    /// ToString, GetHashCode and Equals are forwarded to the object.
    /// </remarks>
    public readonly struct PoolObject<T> : IDisposable
    {
        /// <summary>
        /// Returns the string representation of the pooled object
        /// </summary>
        /// <returns>The string representation of the pooled object, an empty string if it's null</returns>
        public override string ToString() => O?.ToString() ?? "";

        /// <summary>
        /// Returns the hash code of the pooled object
        /// </summary>
        /// <returns>The hash code of the pooled object, zero if it's null</returns>
        public override int GetHashCode() => O?.GetHashCode() ?? 0;

        /// <summary>
        /// Compares the pooled object with another pool object (the pooled objects are compared) or any other object
        /// </summary>
        /// <param name="obj">A PoolObject or an object to compare the pooled object with</param>
        /// <returns>True if the pooled object is equal to the object</returns>
        public override bool Equals(object obj)
        {
            if (obj is PoolObject<T> p)
                obj = p.O;
            var o = O;
            return o is null ? obj is null : o.Equals(obj);
        }

        internal PoolObject(T obj, Action<PoolObject<T>> onDispose)
        {
            O = obj;
            D = onDispose;
        }

        /// <summary>
        /// Get the pooled object
        /// </summary>
        /// <param name="d">The pool object</param>
        public static implicit operator T(PoolObject<T> d) => d.O;

        /// <summary>
        /// Return the object to the pool (must only be called once)
        /// </summary>
        public void Dispose() => D(this);

        internal readonly T O;
        readonly Action<PoolObject<T>> D;

    }

    /// <summary>
    /// An unbounded concurrent object pool, use <see cref="ObjectPool.Create{T}()"/> or <see cref="ObjectPool.Create{T}(Func{T})"/> to create one
    /// </summary>
    /// <typeparam name="T">The type of the pooled objects</typeparam>
    /// <remarks>
    /// The pool is lock free, objects are re-used in LIFO order (the most recently returned object is returned first).
    /// </remarks>
    public sealed class ObjectPool<T> : IDisposable
    {
        internal ObjectPool(Func<T> create)
        {
            New = create;
        }


        /// <summary>
        /// Get an object from the pool, or create a new one if the pool is empty.
        /// Dispose the returned value to return the object to the pool.
        /// </summary>
        /// <returns>A pool object (implicitly converted to the object)</returns>
        /// <remarks>Any exception thrown by the creator function is propagated</remarks>
        public PoolObject<T> Alloc()
        {
            var os = Objs;
            if (os.TryPop(out var v))
                return v;
            return new PoolObject<T>(New(), os.Push);
        }

        readonly Func<T> New;
        readonly ConcurrentStack<PoolObject<T>> Objs = new ConcurrentStack<PoolObject<T>>();

        /// <summary>
        /// Disposes all objects that are currently in the pool (if T implements IDisposable).
        /// Objects in use are not disposed (and are returned to the pool when disposed).
        /// </summary>
        /// <remarks>
        /// Only if the type T itself implements IDisposable objects are disposed (and removed from the pool), else this method does nothing.
        /// </remarks>
        public void Dispose()
        {
            if (!typeof(IDisposable).IsAssignableFrom(typeof(T)))
                return;
            var os = Objs;
            while (os.TryPop(out var v))
                (v.O as IDisposable)?.Dispose();
        }
    }

    /// <summary>
    /// An unbounded concurrent object pool where objects are created async, use <see cref="ObjectPool.CreateAsync{T}(Func{Task{T}})"/> to create one
    /// </summary>
    /// <typeparam name="T">The type of the pooled objects</typeparam>
    /// <remarks>
    /// The pool is lock free, objects are re-used in LIFO order (the most recently returned object is returned first).
    /// </remarks>
    public sealed class AsyncObjectPool<T> : IDisposable
    {
        internal AsyncObjectPool(Func<Task<T>> create)
        {
            New = create;
        }

        /// <summary>
        /// Get an object from the pool, or create a new one if the pool is empty.
        /// Dispose the returned value to return the object to the pool.
        /// </summary>
        /// <returns>A pool object (implicitly converted to the object)</returns>
        /// <remarks>Any exception thrown by the creator function is propagated (in the returned task)</remarks>
        public async Task<PoolObject<T>> Alloc()
        {
            var os = Objs;
            if (os.TryPop(out var v))
                return v;
            return new PoolObject<T>(await New().ConfigureAwait(false), os.Push);
        }

        readonly Func<Task<T>> New;
        readonly ConcurrentStack<PoolObject<T>> Objs = new ConcurrentStack<PoolObject<T>>();

        /// <summary>
        /// Disposes all objects that are currently in the pool (if T implements IDisposable).
        /// Objects in use are not disposed (and are returned to the pool when disposed).
        /// </summary>
        /// <remarks>
        /// Only if the type T itself implements IDisposable objects are disposed (and removed from the pool), else this method does nothing.
        /// </remarks>
        public void Dispose()
        {
            if (!typeof(IDisposable).IsAssignableFrom(typeof(T)))
                return;
            var os = Objs;
            while (os.TryPop(out var v))
                (v.O as IDisposable)?.Dispose();
        }
    }


}
