using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace SysWeaver
{
    /// <summary>
    /// Creates object pools
    /// </summary>
    public static class ObjectPool
    {
        public static ObjectPool<T> Create<T>() where T : new() => new ObjectPool<T>(() => new T());
        public static ObjectPool<T> Create<T>(Func<T> creator) => new ObjectPool<T>(creator);
        public static AsyncObjectPool<T> CreateAsync<T>(Func<Task<T>> creator) => new AsyncObjectPool<T>(creator);

    }

    public readonly struct PoolObject<T> : IDisposable
    {
        public override string ToString() => O.ToString();

        public override int GetHashCode() => O.GetHashCode();

        public override bool Equals(object obj) => obj is PoolObject<T> ? O.Equals(((PoolObject<T>)obj).O) : O.Equals(obj);

        internal PoolObject(T obj, Action<PoolObject<T>> onDispose)
        {
            O = obj;
            D = onDispose;
        }

        public static implicit operator T(PoolObject<T> d) => d.O;

        public void Dispose() => D(this);

        internal readonly T O;
        readonly Action<PoolObject<T>> D;

    }

    public sealed class ObjectPool<T> : IDisposable
    {
        internal ObjectPool(Func<T> create)
        {
            New = create;
        }


        public PoolObject<T> Alloc()
        {
            var os = Objs;
            if (os.TryPop(out var v))
                return v;
            return new PoolObject<T>(New(), os.Push);
        }

        readonly Func<T> New;
        readonly ConcurrentStack<PoolObject<T>> Objs = new ConcurrentStack<PoolObject<T>>();

        public void Dispose()
        {
            if (!(typeof(T) is IDisposable))
                return;
            var os = Objs;
            if (os.TryPop(out var v))
                (v.O as IDisposable).Dispose();
        }
    }

    public sealed class AsyncObjectPool<T> : IDisposable
    {
        internal AsyncObjectPool(Func<Task<T>> create)
        {
            New = create;
        }

        public async Task<PoolObject<T>> Alloc()
        {
            var os = Objs;
            if (os.TryPop(out var v))
                return v;
            return new PoolObject<T>(await New().ConfigureAwait(false), os.Push);
        }

        readonly Func<Task<T>> New;
        readonly ConcurrentStack<PoolObject<T>> Objs = new ConcurrentStack<PoolObject<T>>();

        public void Dispose()
        {
            if (!(typeof(T) is IDisposable))
                return;
            var os = Objs;
            if (os.TryPop(out var v))
                (v.O as IDisposable).Dispose();
        }
    }


}
