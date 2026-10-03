using System;
using System.Threading;

namespace SysWeaver.Compression
{
    /// <summary>
    /// A small, bounded, lock-free and allocation-free pool of reusable instances (such as encoders and decoders).
    /// </summary>
    /// <typeparam name="T">The instance type</typeparam>
    public sealed class CompInstancePool<T> where T : class, IDisposable
    {
        /// <summary>
        /// Create a pool
        /// </summary>
        /// <param name="maxRetained">The max number of instances to keep in the pool, defaults to the number of processors capped at 8</param>
        public CompInstancePool(int maxRetained = 0)
        {
            if (maxRetained <= 0)
                maxRetained = Math.Min(Environment.ProcessorCount, 8);
            Slots = new T[maxRetained];
        }

        readonly T[] Slots;

        /// <summary>
        /// Get an instance from the pool
        /// </summary>
        /// <returns>An instance or null if the pool is empty</returns>
        public T TryRent()
        {
            var slots = Slots;
            var l = slots.Length;
            for (int i = 0; i < l; ++i)
            {
                var x = Interlocked.Exchange(ref slots[i], null);
                if (x != null)
                    return x;
            }
            return null;
        }

        /// <summary>
        /// Return an instance to the pool, the instance is disposed if the pool is full
        /// </summary>
        /// <param name="instance">The instance to return, must be in a reusable state</param>
        public void Return(T instance)
        {
            var slots = Slots;
            var l = slots.Length;
            for (int i = 0; i < l; ++i)
            {
                if (Interlocked.CompareExchange(ref slots[i], instance, null) == null)
                    return;
            }
            instance.Dispose();
        }
    }
}
