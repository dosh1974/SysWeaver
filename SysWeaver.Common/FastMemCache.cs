using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver
{





    /// <summary>
    /// Implements a thread safe cache that removes its items after a fixed duration (or at the time returned by an expiration function).
    /// </summary>
    /// <remarks>
    /// The expiration time of an item is computed when the item is added / updated, reading an item doesn't extend its life time.
    /// Expired items are never returned, they are removed (pruned) from the cache on any write (or when <see cref="Prune"/> is called).
    /// With a fixed duration, pruning walks a queue in insertion order (the expiration times are increasing) and stops at the first item that hasn't expired.
    /// With an expiration function, the expiration times are kept in a priority queue (min-heap), so items are pruned in expiration order (a long lived item doesn't delay the pruning of other items).
    /// Items that never expire (<see cref="DateTime.MaxValue"/>) are not tracked for pruning at all.
    /// Updates of the same key are serialized using a per key spin lock (a lock entry in a concurrent dictionary), so a value factory is only executed once for a key at the same time.
    /// The synchronous methods hold the key lock while the value factory runs (other callers for the key spin / sleep).
    /// The async methods only hold the key lock while starting an update: a running update is a pending entry, callers with waitUntilReady = true await it (without blocking a thread),
    /// callers with waitUntilReady = false get default until it's ready.
    /// Keys can't be null.
    /// The synchronous methods (GetOrUpdate etc) treat a pending entry as a cache hit and return default until the value is ready.
    /// </remarks>
    /// <typeparam name="K">The type of the key</typeparam>
    /// <typeparam name="V">The type of the value</typeparam>
    public sealed class FastMemCache<K, V> : IEnumerable<ValueTuple<DateTime, K, V>>
    {

        /// <summary>
        /// Creates a cache that removes its items after the specified duration (after they are added or updated)
        /// </summary>
        /// <param name="timeout">The duration to keep items in the cache (after they are added or updated).
        /// A zero or negative duration means that items expire immediately (nothing is cached).
        /// A very large duration (like <see cref="TimeSpan.MaxValue"/>) means that items never expire.</param>
        /// <param name="comparer">An optional key comparer, null to use the default comparer</param>
        public FastMemCache(TimeSpan timeout, IEqualityComparer<K> comparer = null)
            : this(GetTimeoutFunc(timeout), comparer, true)
        {
        }

        /// <summary>
        /// Get the expiration function to use for a fixed timeout
        /// </summary>
        /// <param name="timeout">The timeout</param>
        /// <returns>A function that returns the expiration time</returns>
        static Func<V, DateTime> GetTimeoutFunc(TimeSpan timeout)
        {
            //  Adding a large time span to the current time would overflow (throw), clamp to DateTime.MaxValue (never expire)
            var maxTicks = (DateTime.MaxValue.Ticks - DateTime.UtcNow.Ticks) >> 1;
            if (timeout.Ticks < maxTicks)
                return x => DateTime.UtcNow + timeout;
            return x =>
            {
                var now = DateTime.UtcNow;
                return (DateTime.MaxValue - now) > timeout ? now + timeout : DateTime.MaxValue;
            };
        }


        /// <summary>
        /// Creates a cache where the expiration time of every item is computed by a function (when the item is added or updated)
        /// </summary>
        /// <param name="getExpirationTimeUtc">A function that gets the expiration time (as UTC) of a value, called every time a value is added or updated (while holding the key lock).
        /// Return <see cref="DateTime.MaxValue"/> for items that should never expire.
        /// The expiration times doesn't have to be increasing, items are pruned in expiration order (see <see cref="Prune"/>).</param>
        /// <param name="comparer">An optional key comparer, null to use the default comparer</param>
        /// <exception cref="ArgumentNullException"><paramref name="getExpirationTimeUtc"/> is null</exception>
        public FastMemCache(Func<V, DateTime> getExpirationTimeUtc, IEqualityComparer<K> comparer = null)
            : this(getExpirationTimeUtc, comparer, false)
        {
        }

        /// <summary>
        /// Creates a cache
        /// </summary>
        /// <param name="getExpirationTimeUtc">A function that gets the expiration time (as UTC) of a value</param>
        /// <param name="comparer">An optional key comparer, null to use the default comparer</param>
        /// <param name="increasing">True if the expiration times are increasing in insertion order (fixed duration), then the cheaper (lock free) FIFO queue is used for pruning,
        /// else a priority queue is used</param>
        FastMemCache(Func<V, DateTime> getExpirationTimeUtc, IEqualityComparer<K> comparer, bool increasing)
        {
            ArgumentNullException.ThrowIfNull(getExpirationTimeUtc);
            GetExpirationDate = getExpirationTimeUtc;
            if (!increasing)
                H = new PriorityQueue<ValueTuple<DateTime, K>, DateTime>();
            if (comparer == null)
            {
                C = new ConcurrentDictionary<K, (DateTime, V, Task<V>)>();
                Locks = new ConcurrentDictionary<K, int>();
            }
            else
            {
                C = new ConcurrentDictionary<K, (DateTime, V, Task<V>)>(comparer);
                Locks = new ConcurrentDictionary<K, int>(comparer);
            }
        }

        /// <summary>
        /// Set a new value (adds a new item or replaces an existing item)
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="value">The new value</param>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is null</exception>
        /// <remarks>Must not be called for the same key from within a value factory (dead lock).</remarks>
        public void Set(K key, V value)
        {
            var c = C;
            Lock(key);
            try
            {
                var val = ValueTuple.Create(GetExpirationDate(value), value, (Task<V>)null);
                c[key] = val;
                Track(val.Item1, key);
            }
            finally
            {
                Unlock(key);
            }
        }

        /// <summary>
        /// Try add new value
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="value">The new value</param>
        /// <returns>True if the value was added, false if a (non-expired) item with the key already exist (the value is not updated)</returns>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is null</exception>
        /// <remarks>An expired item (that haven't been pruned yet) is treated as missing (and is replaced).
        /// Must not be called for the same key from within a value factory (dead lock).</remarks>
        public bool TryAdd(K key, V value)
        {
            var c = C;
            Lock(key);
            try
            {
                var val = ValueTuple.Create(GetExpirationDate(value), value, (Task<V>)null);
                if (!c.TryAdd(key, val))
                {
                    //  An expired entry (not pruned yet) is missing (like TryGet), replace it
                    if (c.TryGetValue(key, out var existing) && (DateTime.UtcNow < existing.Item1))
                        return false;
                    c[key] = val;
                }
                Track(val.Item1, key);
                return true;
            }
            finally
            {
                Unlock(key);
            }
        }

        /// <summary>
        /// Get an item if it's cached
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="value">The cached value or default it it doesn't exist</param>
        /// <returns>True if a (non-expired) value exist</returns>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is null</exception>
        /// <remarks>An item that is being created in the background (async methods with waitUntilReady = false) doesn't exist until it's completed.
        /// Doesn't affect the hit / miss statistics.</remarks>
        public bool TryGet(K key, out V value)
        {
            if (!C.TryGetValue(key, out var v))
            {
                value = default;
                return false;
            }
            if ((DateTime.UtcNow < v.Item1) && (v.Item3 == null))
            {
                value = v.Item2;
                return true;
            }
            value = default;
            return false;
        }

        #region With exising


        /// <summary>
        /// Get an item from the cache, if it doesn't exist in the cache, the supplied delegate is executed to create the item.
        /// Only one item can be created at the same time (locked using the key), so no risk for "double" effort. 
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="func">The delegate used to create a non-existing item</param>
        /// <returns>The value of the item</returns>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is null, or <paramref name="func"/> is null and the item have to be created (for <paramref name="func"/>: debug builds only; release builds throw a <see cref="NullReferenceException"/>)</exception>
        /// <remarks>
        /// Any exception thrown by <paramref name="func"/> is propagated to the caller (nothing is cached).
        /// The key is locked while <paramref name="func"/> executes, so <paramref name="func"/> may use the cache, but must not request the same key (dead lock).
        /// The existing value passed to <paramref name="func"/> is the expired value (if it still exist in the cache) or default.
        /// </remarks>
        public V GetOrUpdateWithExisting(K key, Func<K, V, V> func)
        {
            var c = C;
            if (c.TryGetValue(key, out var val))
            {
                if (DateTime.UtcNow < val.Item1)
                {
                    Interlocked.Increment(ref HitCount);
                    return val.Item2;
                }
            }
            Lock(key);
            try
            {
                //  Test if someone else added this cache entry
                if (c.TryGetValue(key, out val))
                {
                    if (DateTime.UtcNow < val.Item1)
                    {
                        Interlocked.Increment(ref SemiHitCount);
                        return val.Item2;
                    }
                }
                Interlocked.Increment(ref MissCount);
#if DEBUG
                ArgumentNullException.ThrowIfNull(func);
#endif//DEBUG
                var value = func(key, val.Item2);
                val = ValueTuple.Create(GetExpirationDate(value), value, (Task<V>)null);
                c[key] = val;
                Track(val.Item1, key);
                return val.Item2;
            }
            finally
            {
                Unlock(key);
            }
        }

        /// <summary>
        /// Get an item from the cache, if it doesn't exist in the cache, the supplied delegate is executed to create the item.
        /// Only one item can be created at the same time (locked using the key), so no risk for "double" effort. 
        /// </summary>
        /// <typeparam name="A">The type of the custom argument</typeparam>
        /// <param name="key">The key</param>
        /// <param name="func">The delegate used to create a non-existing item</param>
        /// <param name="arg">A custom argument that is passed to the delegate if invoked</param>
        /// <returns>The value of the item</returns>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is null, or <paramref name="func"/> is null and the item have to be created (for <paramref name="func"/>: debug builds only; release builds throw a <see cref="NullReferenceException"/>)</exception>
        /// <remarks>
        /// Any exception thrown by <paramref name="func"/> is propagated to the caller (nothing is cached).
        /// The key is locked while <paramref name="func"/> executes, so <paramref name="func"/> may use the cache, but must not request the same key (dead lock).
        /// The existing value passed to <paramref name="func"/> is the expired value (if it still exist in the cache) or default.
        /// </remarks>
        public V GetOrUpdateWithExisting<A>(K key, Func<K, V, A, V> func, A arg)
        {
            var c = C;
            if (c.TryGetValue(key, out var val))
            {
                if (DateTime.UtcNow < val.Item1)
                {
                    Interlocked.Increment(ref HitCount);
                    return val.Item2;
                }
            }
            Lock(key);
            try
            {
                //  Test if someone else added this cache entry
                if (c.TryGetValue(key, out val))
                {
                    if (DateTime.UtcNow < val.Item1)
                    {
                        Interlocked.Increment(ref SemiHitCount);
                        return val.Item2;
                    }
                }
                Interlocked.Increment(ref MissCount);
#if DEBUG
                ArgumentNullException.ThrowIfNull(func);
#endif//DEBUG
                var value = func(key, val.Item2, arg);
                val = ValueTuple.Create(GetExpirationDate(value), value, (Task<V>)null);
                c[key] = val;
                Track(val.Item1, key);
                return val.Item2;
            }
            finally
            {
                Unlock(key);
            }
        }


        /// <summary>
        /// Get an item from the cache, if it doesn't exist in the cache, the supplied delegate is executed to create the item.
        /// Only one item can be created at the same time (locked using the key), so no risk for "double" effort. 
        /// </summary>
        /// <typeparam name="A0">The type of the first custom argument</typeparam>
        /// <typeparam name="A1">The type of the second custom argument</typeparam>
        /// <param name="key">The key</param>
        /// <param name="func">The delegate used to create a non-existing item</param>
        /// <param name="arg0">A custom argument that is passed to the delegate if invoked</param>
        /// <param name="arg1">A custom argument that is passed to the delegate if invoked</param>
        /// <returns>The value of the item</returns>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is null, or <paramref name="func"/> is null and the item have to be created (for <paramref name="func"/>: debug builds only; release builds throw a <see cref="NullReferenceException"/>)</exception>
        /// <remarks>
        /// Any exception thrown by <paramref name="func"/> is propagated to the caller (nothing is cached).
        /// The key is locked while <paramref name="func"/> executes, so <paramref name="func"/> may use the cache, but must not request the same key (dead lock).
        /// The existing value passed to <paramref name="func"/> is the expired value (if it still exist in the cache) or default.
        /// </remarks>
        public V GetOrUpdateWithExisting<A0, A1>(K key, Func<K, V, A0, A1, V> func, A0 arg0, A1 arg1)
        {
            var c = C;
            if (c.TryGetValue(key, out var val))
            {
                if (DateTime.UtcNow < val.Item1)
                {
                    Interlocked.Increment(ref HitCount);
                    return val.Item2;
                }
            }
            Lock(key);
            try
            {
                //  Test if someone else added this cache entry
                if (c.TryGetValue(key, out val))
                {
                    if (DateTime.UtcNow < val.Item1)
                    {
                        Interlocked.Increment(ref SemiHitCount);
                        return val.Item2;
                    }
                }
                Interlocked.Increment(ref MissCount);
#if DEBUG
                ArgumentNullException.ThrowIfNull(func);
#endif//DEBUG
                var value = func(key, val.Item2, arg0, arg1);
                val = ValueTuple.Create(GetExpirationDate(value), value, (Task<V>)null);
                c[key] = val;
                Track(val.Item1, key);
                return val.Item2;
            }
            finally
            {
                Unlock(key);
            }
        }


        #region Task

         /// <summary>
        /// Get an item from the cache, if it doesn't exist in the cache, the supplied delegate is executed to create the item.
        /// Only one item can be created at the same time (locked using the key), so no risk for "double" effort. 
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="func">The delegate used to create a non-existing item</param>
        /// <param name="waitUntilReady">If the item have to be updated, wait for the update before returning, else the default value will be returned and the update will be started concurrently</param>
        /// <returns>The value of the item or default if wait until ready is false and the update haven't completed yet</returns>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is null, or <paramref name="func"/> is null and the item have to be created (the returned task is faulted)</exception>
        /// <remarks>
        /// Any exception thrown by <paramref name="func"/> is propagated to the caller (nothing is cached).
        /// Only one update per key runs at a time, other callers wait for it (without blocking a thread) or get default (see <paramref name="waitUntilReady"/>). <paramref name="func"/> may use the cache, but must not request the same key (dead lock).
        /// The existing value passed to <paramref name="func"/> is the expired value (if it still exist in the cache) or default.
        /// If <paramref name="waitUntilReady"/> is false and <paramref name="func"/> doesn't complete synchronously, the update continues in the background, callers that doesn't wait will get default until it completes.
        /// If an update fails, the caller that started it gets the exception, other callers waiting for it try again (one at a time), or get the exception if it was a background update.
        /// </remarks>
        public ValueTask<V> GetOrUpdateWithExistingAsync(K key, Func<K, V, Task<V>> func, bool waitUntilReady = true)
        {
            if (C.TryGetValue(key, out var val))
            {
                if (DateTime.UtcNow < val.Item1)
                {
                    var task = val.Item3;
                    //  A running update is waited for (and retried if it fails) by the internal method
                    if ((!waitUntilReady) || (task == null) || task.IsCompletedSuccessfully)
                    {
                        Interlocked.Increment(ref HitCount);
                        return ValueTask.FromResult(((task != null) && task.IsCompletedSuccessfully) ? task.Result : val.Item2);
                    }
                }
            }
            return InternalGetOrUpdateAsync(key, func, waitUntilReady);
        }

 
        /// <summary>
        /// Get an item from the cache, if it doesn't exist in the cache, the supplied delegate is executed to create the item.
        /// Only one item can be created at the same time (locked using the key), so no risk for "double" effort. 
        /// </summary>
        /// <typeparam name="A">The type of the custom argument</typeparam>
        /// <param name="key">The key</param>
        /// <param name="func">The delegate used to create a non-existing item</param>
        /// <param name="arg">A custom argument that is passed to the delegate if invoked</param>
        /// <param name="waitUntilReady">If the item have to be updated, wait for the update before returning, else the default value will be returned and the update will be started concurrently</param>
        /// <returns>The value of the item or default if wait until ready is false and the update haven't completed yet</returns>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is null, or <paramref name="func"/> is null and the item have to be created (the returned task is faulted)</exception>
        /// <remarks>
        /// Any exception thrown by <paramref name="func"/> is propagated to the caller (nothing is cached).
        /// Only one update per key runs at a time, other callers wait for it (without blocking a thread) or get default (see <paramref name="waitUntilReady"/>). <paramref name="func"/> may use the cache, but must not request the same key (dead lock).
        /// The existing value passed to <paramref name="func"/> is the expired value (if it still exist in the cache) or default.
        /// If <paramref name="waitUntilReady"/> is false and <paramref name="func"/> doesn't complete synchronously, the update continues in the background, callers that doesn't wait will get default until it completes.
        /// If an update fails, the caller that started it gets the exception, other callers waiting for it try again (one at a time), or get the exception if it was a background update.
        /// </remarks>
        public ValueTask<V> GetOrUpdateWithExistingAsync<A>(K key, Func<K, V, A, Task<V>> func, A arg, bool waitUntilReady = true)
        {
            if (C.TryGetValue(key, out var val))
            {
                if (DateTime.UtcNow < val.Item1)
                {
                    var task = val.Item3;
                    //  A running update is waited for (and retried if it fails) by the internal method
                    if ((!waitUntilReady) || (task == null) || task.IsCompletedSuccessfully)
                    {
                        Interlocked.Increment(ref HitCount);
                        return ValueTask.FromResult(((task != null) && task.IsCompletedSuccessfully) ? task.Result : val.Item2);
                    }
                }
            }
            return InternalGetOrUpdateAsync(key, func, arg, waitUntilReady);
        }



        /// <summary>
        /// Get an item from the cache, if it doesn't exist in the cache, the supplied delegate is executed to create the item.
        /// Only one item can be created at the same time (locked using the key), so no risk for "double" effort. 
        /// </summary>
        /// <typeparam name="A0">The type of the first custom argument</typeparam>
        /// <typeparam name="A1">The type of the second custom argument</typeparam>
        /// <param name="key">The key</param>
        /// <param name="func">The delegate used to create a non-existing item</param>
        /// <param name="arg0">A custom argument that is passed to the delegate if invoked</param>
        /// <param name="arg1">A custom argument that is passed to the delegate if invoked</param>
        /// <param name="waitUntilReady">If the item have to be updated, wait for the update before returning, else the default value will be returned and the update will be started concurrently</param>
        /// <returns>The value of the item or default if wait until ready is false and the update haven't completed yet</returns>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is null, or <paramref name="func"/> is null and the item have to be created (the returned task is faulted)</exception>
        /// <remarks>
        /// Any exception thrown by <paramref name="func"/> is propagated to the caller (nothing is cached).
        /// Only one update per key runs at a time, other callers wait for it (without blocking a thread) or get default (see <paramref name="waitUntilReady"/>). <paramref name="func"/> may use the cache, but must not request the same key (dead lock).
        /// The existing value passed to <paramref name="func"/> is the expired value (if it still exist in the cache) or default.
        /// If <paramref name="waitUntilReady"/> is false and <paramref name="func"/> doesn't complete synchronously, the update continues in the background, callers that doesn't wait will get default until it completes.
        /// If an update fails, the caller that started it gets the exception, other callers waiting for it try again (one at a time), or get the exception if it was a background update.
        /// </remarks>
        public ValueTask<V> GetOrUpdateWithExistingAsync<A0, A1>(K key, Func<K, V, A0, A1, Task<V>> func, A0 arg0, A1 arg1, bool waitUntilReady = true)
        {
            if (C.TryGetValue(key, out var val))
            {
                if (DateTime.UtcNow < val.Item1)
                {
                    var task = val.Item3;
                    //  A running update is waited for (and retried if it fails) by the internal method
                    if ((!waitUntilReady) || (task == null) || task.IsCompletedSuccessfully)
                    {
                        Interlocked.Increment(ref HitCount);
                        return ValueTask.FromResult(((task != null) && task.IsCompletedSuccessfully) ? task.Result : val.Item2);
                    }
                }
            }
            return InternalGetOrUpdateAsync(key, func, arg0, arg1, waitUntilReady);
        }


        #endregion//Task

        #region ValueTask

        /// <summary>
        /// Get an item from the cache, if it doesn't exist in the cache, the supplied delegate is executed to create the item.
        /// Only one item can be created at the same time (locked using the key), so no risk for "double" effort. 
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="func">The delegate used to create a non-existing item</param>
        /// <param name="waitUntilReady">If the item have to be updated, wait for the update before returning, else the default value will be returned and the update will be started concurrently</param>
        /// <returns>The value of the item or default if wait until ready is false and the update haven't completed yet</returns>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is null, or <paramref name="func"/> is null and the item have to be created (the returned task is faulted)</exception>
        /// <remarks>
        /// Any exception thrown by <paramref name="func"/> is propagated to the caller (nothing is cached).
        /// Only one update per key runs at a time, other callers wait for it (without blocking a thread) or get default (see <paramref name="waitUntilReady"/>). <paramref name="func"/> may use the cache, but must not request the same key (dead lock).
        /// The existing value passed to <paramref name="func"/> is the expired value (if it still exist in the cache) or default.
        /// If <paramref name="waitUntilReady"/> is false and <paramref name="func"/> doesn't complete synchronously, the update continues in the background, callers that doesn't wait will get default until it completes.
        /// If an update fails, the caller that started it gets the exception, other callers waiting for it try again (one at a time), or get the exception if it was a background update.
        /// </remarks>
        public ValueTask<V> GetOrUpdateWithExistingValueAsync(K key, Func<K, V, ValueTask<V>> func, bool waitUntilReady = true)
        {
            if (C.TryGetValue(key, out var val))
            {
                if (DateTime.UtcNow < val.Item1)
                {
                    var task = val.Item3;
                    //  A running update is waited for (and retried if it fails) by the internal method
                    if ((!waitUntilReady) || (task == null) || task.IsCompletedSuccessfully)
                    {
                        Interlocked.Increment(ref HitCount);
                        return ValueTask.FromResult(((task != null) && task.IsCompletedSuccessfully) ? task.Result : val.Item2);
                    }
                }
            }
            return InternalGetOrUpdateAsync(key, func, waitUntilReady);
        }


        /// <summary>
        /// Get an item from the cache, if it doesn't exist in the cache, the supplied delegate is executed to create the item.
        /// Only one item can be created at the same time (locked using the key), so no risk for "double" effort. 
        /// </summary>
        /// <typeparam name="A">The type of the custom argument</typeparam>
        /// <param name="key">The key</param>
        /// <param name="func">The delegate used to create a non-existing item</param>
        /// <param name="arg">A custom argument that is passed to the delegate if invoked</param>
        /// <param name="waitUntilReady">If the item have to be updated, wait for the update before returning, else the default value will be returned and the update will be started concurrently</param>
        /// <returns>The value of the item or default if wait until ready is false and the update haven't completed yet</returns>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is null, or <paramref name="func"/> is null and the item have to be created (the returned task is faulted)</exception>
        /// <remarks>
        /// Any exception thrown by <paramref name="func"/> is propagated to the caller (nothing is cached).
        /// Only one update per key runs at a time, other callers wait for it (without blocking a thread) or get default (see <paramref name="waitUntilReady"/>). <paramref name="func"/> may use the cache, but must not request the same key (dead lock).
        /// The existing value passed to <paramref name="func"/> is the expired value (if it still exist in the cache) or default.
        /// If <paramref name="waitUntilReady"/> is false and <paramref name="func"/> doesn't complete synchronously, the update continues in the background, callers that doesn't wait will get default until it completes.
        /// If an update fails, the caller that started it gets the exception, other callers waiting for it try again (one at a time), or get the exception if it was a background update.
        /// </remarks>
        public ValueTask<V> GetOrUpdateWithExistingValueAsync<A>(K key, Func<K, V, A, ValueTask<V>> func, A arg, bool waitUntilReady = true)
        {
            if (C.TryGetValue(key, out var val))
            {
                if (DateTime.UtcNow < val.Item1)
                {
                    var task = val.Item3;
                    //  A running update is waited for (and retried if it fails) by the internal method
                    if ((!waitUntilReady) || (task == null) || task.IsCompletedSuccessfully)
                    {
                        Interlocked.Increment(ref HitCount);
                        return ValueTask.FromResult(((task != null) && task.IsCompletedSuccessfully) ? task.Result : val.Item2);
                    }
                }
            }
            return InternalGetOrUpdateAsync(key, func, arg, waitUntilReady);
        }



        /// <summary>
        /// Get an item from the cache, if it doesn't exist in the cache, the supplied delegate is executed to create the item.
        /// Only one item can be created at the same time (locked using the key), so no risk for "double" effort. 
        /// </summary>
        /// <typeparam name="A0">The type of the first custom argument</typeparam>
        /// <typeparam name="A1">The type of the second custom argument</typeparam>
        /// <param name="key">The key</param>
        /// <param name="func">The delegate used to create a non-existing item</param>
        /// <param name="arg0">A custom argument that is passed to the delegate if invoked</param>
        /// <param name="arg1">A custom argument that is passed to the delegate if invoked</param>
        /// <param name="waitUntilReady">If the item have to be updated, wait for the update before returning, else the default value will be returned and the update will be started concurrently</param>
        /// <returns>The value of the item or default if wait until ready is false and the update haven't completed yet</returns>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is null, or <paramref name="func"/> is null and the item have to be created (the returned task is faulted)</exception>
        /// <remarks>
        /// Any exception thrown by <paramref name="func"/> is propagated to the caller (nothing is cached).
        /// Only one update per key runs at a time, other callers wait for it (without blocking a thread) or get default (see <paramref name="waitUntilReady"/>). <paramref name="func"/> may use the cache, but must not request the same key (dead lock).
        /// The existing value passed to <paramref name="func"/> is the expired value (if it still exist in the cache) or default.
        /// If <paramref name="waitUntilReady"/> is false and <paramref name="func"/> doesn't complete synchronously, the update continues in the background, callers that doesn't wait will get default until it completes.
        /// If an update fails, the caller that started it gets the exception, other callers waiting for it try again (one at a time), or get the exception if it was a background update.
        /// </remarks>
        public ValueTask<V> GetOrUpdateWithExistingValueAsync<A0, A1>(K key, Func<K, V, A0, A1, ValueTask<V>> func, A0 arg0, A1 arg1, bool waitUntilReady = true)
        {
            if (C.TryGetValue(key, out var val))
            {
                if (DateTime.UtcNow < val.Item1)
                {
                    var task = val.Item3;
                    //  A running update is waited for (and retried if it fails) by the internal method
                    if ((!waitUntilReady) || (task == null) || task.IsCompletedSuccessfully)
                    {
                        Interlocked.Increment(ref HitCount);
                        return ValueTask.FromResult(((task != null) && task.IsCompletedSuccessfully) ? task.Result : val.Item2);
                    }
                }
            }
            return InternalGetOrUpdateAsync(key, func, arg0, arg1, waitUntilReady);
        }

        #endregion//ValueTask

        #endregion//With exising


        #region Without exising

        #region Sync

        /// <summary>
        /// Get an item from the cache, if it doesn't exist in the cache, the supplied delegate is executed to create the item.
        /// Only one item can be created at the same time (locked using the key), so no risk for "double" effort. 
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="func">The delegate used to create a non-existing item</param>
        /// <returns>The value of the item</returns>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is null, or <paramref name="func"/> is null and the item have to be created (for <paramref name="func"/>: debug builds only; release builds throw a <see cref="NullReferenceException"/>)</exception>
        /// <remarks>
        /// Any exception thrown by <paramref name="func"/> is propagated to the caller (nothing is cached).
        /// The key is locked while <paramref name="func"/> executes, so <paramref name="func"/> may use the cache, but must not request the same key (dead lock).
        /// </remarks>
        public V GetOrUpdate(K key, Func<K, V> func)
        {
            var c = C;
            if (c.TryGetValue(key, out var val))
            {
                if (DateTime.UtcNow < val.Item1)
                {
                    Interlocked.Increment(ref HitCount);
                    return val.Item2;
                }
            }
            Lock(key);
            try
            {
                //  Test if someone else added this cache entry
                if (c.TryGetValue(key, out val))
                {
                    if (DateTime.UtcNow < val.Item1)
                    {
                        Interlocked.Increment(ref SemiHitCount);
                        return val.Item2;
                    }
                }
                Interlocked.Increment(ref MissCount);
#if DEBUG
                ArgumentNullException.ThrowIfNull(func);
#endif//DEBUG
                var value = func(key);
                val = ValueTuple.Create(GetExpirationDate(value), value, (Task<V>)null);
                c[key] = val;
                Track(val.Item1, key);
                return val.Item2;
            }
            finally
            {
                Unlock(key);
            }
        }

        /// <summary>
        /// Get an item from the cache, if it doesn't exist in the cache, the supplied delegate is executed to create the item.
        /// Only one item can be created at the same time (locked using the key), so no risk for "double" effort. 
        /// </summary>
        /// <typeparam name="A">The type of the custom argument</typeparam>
        /// <param name="key">The key</param>
        /// <param name="func">The delegate used to create a non-existing item</param>
        /// <param name="arg">A custom argument that is passed to the delegate if invoked</param>
        /// <returns>The value of the item</returns>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is null, or <paramref name="func"/> is null and the item have to be created (for <paramref name="func"/>: debug builds only; release builds throw a <see cref="NullReferenceException"/>)</exception>
        /// <remarks>
        /// Any exception thrown by <paramref name="func"/> is propagated to the caller (nothing is cached).
        /// The key is locked while <paramref name="func"/> executes, so <paramref name="func"/> may use the cache, but must not request the same key (dead lock).
        /// </remarks>
        public V GetOrUpdate<A>(K key, Func<K, A, V> func, A arg)
        {
            var c = C;
            if (c.TryGetValue(key, out var val))
            {
                if (DateTime.UtcNow < val.Item1)
                {
                    Interlocked.Increment(ref HitCount);
                    return val.Item2;
                }
            }
            Lock(key);
            try
            {
                //  Test if someone else added this cache entry
                if (c.TryGetValue(key, out val))
                {
                    if (DateTime.UtcNow < val.Item1)
                    {
                        Interlocked.Increment(ref SemiHitCount);
                        return val.Item2;
                    }
                }
                Interlocked.Increment(ref MissCount);
#if DEBUG
                ArgumentNullException.ThrowIfNull(func);
#endif//DEBUG
                var value = func(key, arg);
                val = ValueTuple.Create(GetExpirationDate(value), value, (Task<V>)null);
                c[key] = val;
                Track(val.Item1, key);
                return val.Item2;
            }
            finally
            {
                Unlock(key);
            }
        }


        /// <summary>
        /// Get an item from the cache, if it doesn't exist in the cache, the supplied delegate is executed to create the item.
        /// Only one item can be created at the same time (locked using the key), so no risk for "double" effort. 
        /// </summary>
        /// <typeparam name="A0">The type of the first custom argument</typeparam>
        /// <typeparam name="A1">The type of the second custom argument</typeparam>
        /// <param name="key">The key</param>
        /// <param name="func">The delegate used to create a non-existing item</param>
        /// <param name="arg0">A custom argument that is passed to the delegate if invoked</param>
        /// <param name="arg1">A custom argument that is passed to the delegate if invoked</param>
        /// <returns>The value of the item</returns>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is null, or <paramref name="func"/> is null and the item have to be created (for <paramref name="func"/>: debug builds only; release builds throw a <see cref="NullReferenceException"/>)</exception>
        /// <remarks>
        /// Any exception thrown by <paramref name="func"/> is propagated to the caller (nothing is cached).
        /// The key is locked while <paramref name="func"/> executes, so <paramref name="func"/> may use the cache, but must not request the same key (dead lock).
        /// </remarks>
        public V GetOrUpdate<A0, A1>(K key, Func<K, A0, A1, V> func, A0 arg0, A1 arg1)
        {
            var c = C;
            if (c.TryGetValue(key, out var val))
            {
                if (DateTime.UtcNow < val.Item1)
                {
                    Interlocked.Increment(ref HitCount);
                    return val.Item2;
                }
            }
            Lock(key);
            try
            {
                //  Test if someone else added this cache entry
                if (c.TryGetValue(key, out val))
                {
                    if (DateTime.UtcNow < val.Item1)
                    {
                        Interlocked.Increment(ref SemiHitCount);
                        return val.Item2;
                    }
                }
                Interlocked.Increment(ref MissCount);
#if DEBUG
                ArgumentNullException.ThrowIfNull(func);
#endif//DEBUG
                var value = func(key, arg0, arg1);
                val = ValueTuple.Create(GetExpirationDate(value), value, (Task<V>)null);
                c[key] = val;
                Track(val.Item1, key);
                return val.Item2;
            }
            finally
            {
                Unlock(key);
            }
        }


        #endregion // Sync

        #region Task

        /// <summary>
        /// Get an item from the cache, if it doesn't exist in the cache, the supplied delegate is executed to create the item.
        /// Only one item can be created at the same time (locked using the key), so no risk for "double" effort. 
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="func">The delegate used to create a non-existing item</param>
        /// <param name="waitUntilReady">If the item have to be updated, wait for the update before returning, else the default value will be returned and the update will be started concurrently</param>
        /// <returns>The value of the item or default if wait until ready is false and the update haven't completed yet</returns>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is null, or <paramref name="func"/> is null and the item have to be created (the returned task is faulted)</exception>
        /// <remarks>
        /// Any exception thrown by <paramref name="func"/> is propagated to the caller (nothing is cached).
        /// Only one update per key runs at a time, other callers wait for it (without blocking a thread) or get default (see <paramref name="waitUntilReady"/>). <paramref name="func"/> may use the cache, but must not request the same key (dead lock).
        /// If <paramref name="waitUntilReady"/> is false and <paramref name="func"/> doesn't complete synchronously, the update continues in the background, callers that doesn't wait will get default until it completes.
        /// If an update fails, the caller that started it gets the exception, other callers waiting for it try again (one at a time), or get the exception if it was a background update.
        /// </remarks>
        public ValueTask<V> GetOrUpdateAsync(K key, Func<K, Task<V>> func, bool waitUntilReady = true)
        {
            if (C.TryGetValue(key, out var val))
            {
                if (DateTime.UtcNow < val.Item1)
                {
                    var task = val.Item3;
                    //  A running update is waited for (and retried if it fails) by the internal method
                    if ((!waitUntilReady) || (task == null) || task.IsCompletedSuccessfully)
                    {
                        Interlocked.Increment(ref HitCount);
                        return ValueTask.FromResult(((task != null) && task.IsCompletedSuccessfully) ? task.Result : val.Item2);
                    }
                }
            }
            return InternalGetOrUpdateAsync(key, func, waitUntilReady);
        }


        /// <summary>
        /// Get an item from the cache, if it doesn't exist in the cache, the supplied delegate is executed to create the item.
        /// Only one item can be created at the same time (locked using the key), so no risk for "double" effort. 
        /// </summary>
        /// <typeparam name="A">The type of the custom argument</typeparam>
        /// <param name="key">The key</param>
        /// <param name="func">The delegate used to create a non-existing item</param>
        /// <param name="arg">A custom argument that is passed to the delegate if invoked</param>
        /// <param name="waitUntilReady">If the item have to be updated, wait for the update before returning, else the default value will be returned and the update will be started concurrently</param>
        /// <returns>The value of the item or default if wait until ready is false and the update haven't completed yet</returns>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is null, or <paramref name="func"/> is null and the item have to be created (the returned task is faulted)</exception>
        /// <remarks>
        /// Any exception thrown by <paramref name="func"/> is propagated to the caller (nothing is cached).
        /// Only one update per key runs at a time, other callers wait for it (without blocking a thread) or get default (see <paramref name="waitUntilReady"/>). <paramref name="func"/> may use the cache, but must not request the same key (dead lock).
        /// If <paramref name="waitUntilReady"/> is false and <paramref name="func"/> doesn't complete synchronously, the update continues in the background, callers that doesn't wait will get default until it completes.
        /// If an update fails, the caller that started it gets the exception, other callers waiting for it try again (one at a time), or get the exception if it was a background update.
        /// </remarks>
        public ValueTask<V> GetOrUpdateAsync<A>(K key, Func<K, A, Task<V>> func, A arg, bool waitUntilReady = true)
        {
            if (C.TryGetValue(key, out var val))
            {
                if (DateTime.UtcNow < val.Item1)
                {
                    var task = val.Item3;
                    //  A running update is waited for (and retried if it fails) by the internal method
                    if ((!waitUntilReady) || (task == null) || task.IsCompletedSuccessfully)
                    {
                        Interlocked.Increment(ref HitCount);
                        return ValueTask.FromResult(((task != null) && task.IsCompletedSuccessfully) ? task.Result : val.Item2);
                    }
                }
            }
            return InternalGetOrUpdateAsync(key, func, arg, waitUntilReady);
        }



        /// <summary>
        /// Get an item from the cache, if it doesn't exist in the cache, the supplied delegate is executed to create the item.
        /// Only one item can be created at the same time (locked using the key), so no risk for "double" effort. 
        /// </summary>
        /// <typeparam name="A0">The type of the first custom argument</typeparam>
        /// <typeparam name="A1">The type of the second custom argument</typeparam>
        /// <param name="key">The key</param>
        /// <param name="func">The delegate used to create a non-existing item</param>
        /// <param name="arg0">A custom argument that is passed to the delegate if invoked</param>
        /// <param name="arg1">A custom argument that is passed to the delegate if invoked</param>
        /// <param name="waitUntilReady">If the item have to be updated, wait for the update before returning, else the default value will be returned and the update will be started concurrently</param>
        /// <returns>The value of the item or default if wait until ready is false and the update haven't completed yet</returns>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is null, or <paramref name="func"/> is null and the item have to be created (the returned task is faulted)</exception>
        /// <remarks>
        /// Any exception thrown by <paramref name="func"/> is propagated to the caller (nothing is cached).
        /// Only one update per key runs at a time, other callers wait for it (without blocking a thread) or get default (see <paramref name="waitUntilReady"/>). <paramref name="func"/> may use the cache, but must not request the same key (dead lock).
        /// If <paramref name="waitUntilReady"/> is false and <paramref name="func"/> doesn't complete synchronously, the update continues in the background, callers that doesn't wait will get default until it completes.
        /// If an update fails, the caller that started it gets the exception, other callers waiting for it try again (one at a time), or get the exception if it was a background update.
        /// </remarks>
        public ValueTask<V> GetOrUpdateAsync<A0, A1>(K key, Func<K, A0, A1, Task<V>> func, A0 arg0, A1 arg1, bool waitUntilReady = true)
        {
            if (C.TryGetValue(key, out var val))
            {
                if (DateTime.UtcNow < val.Item1)
                {
                    var task = val.Item3;
                    //  A running update is waited for (and retried if it fails) by the internal method
                    if ((!waitUntilReady) || (task == null) || task.IsCompletedSuccessfully)
                    {
                        Interlocked.Increment(ref HitCount);
                        return ValueTask.FromResult(((task != null) && task.IsCompletedSuccessfully) ? task.Result : val.Item2);
                    }
                }
            }
            return InternalGetOrUpdateAsync(key, func, arg0, arg1, waitUntilReady);
        }


        #endregion//Task

        #region ValueTask

        /// <summary>
        /// Get an item from the cache, if it doesn't exist in the cache, the supplied delegate is executed to create the item.
        /// Only one item can be created at the same time (locked using the key), so no risk for "double" effort. 
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="func">The delegate used to create a non-existing item</param>
        /// <param name="waitUntilReady">If the item have to be updated, wait for the update before returning, else the default value will be returned and the update will be started concurrently</param>
        /// <returns>The value of the item or default if wait until ready is false and the update haven't completed yet</returns>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is null, or <paramref name="func"/> is null and the item have to be created (the returned task is faulted)</exception>
        /// <remarks>
        /// Any exception thrown by <paramref name="func"/> is propagated to the caller (nothing is cached).
        /// Only one update per key runs at a time, other callers wait for it (without blocking a thread) or get default (see <paramref name="waitUntilReady"/>). <paramref name="func"/> may use the cache, but must not request the same key (dead lock).
        /// If <paramref name="waitUntilReady"/> is false and <paramref name="func"/> doesn't complete synchronously, the update continues in the background, callers that doesn't wait will get default until it completes.
        /// If an update fails, the caller that started it gets the exception, other callers waiting for it try again (one at a time), or get the exception if it was a background update.
        /// </remarks>
        public ValueTask<V> GetOrUpdateValueAsync(K key, Func<K, ValueTask<V>> func, bool waitUntilReady = true)
        {
            if (C.TryGetValue(key, out var val))
            {
                if (DateTime.UtcNow < val.Item1)
                {
                    var task = val.Item3;
                    //  A running update is waited for (and retried if it fails) by the internal method
                    if ((!waitUntilReady) || (task == null) || task.IsCompletedSuccessfully)
                    {
                        Interlocked.Increment(ref HitCount);
                        return ValueTask.FromResult(((task != null) && task.IsCompletedSuccessfully) ? task.Result : val.Item2);
                    }
                }
            }
            return InternalGetOrUpdateAsync(key, func, waitUntilReady);
        }


        /// <summary>
        /// Get an item from the cache, if it doesn't exist in the cache, the supplied delegate is executed to create the item.
        /// Only one item can be created at the same time (locked using the key), so no risk for "double" effort. 
        /// </summary>
        /// <typeparam name="A">The type of the custom argument</typeparam>
        /// <param name="key">The key</param>
        /// <param name="func">The delegate used to create a non-existing item</param>
        /// <param name="arg">A custom argument that is passed to the delegate if invoked</param>
        /// <param name="waitUntilReady">If the item have to be updated, wait for the update before returning, else the default value will be returned and the update will be started concurrently</param>
        /// <returns>The value of the item or default if wait until ready is false and the update haven't completed yet</returns>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is null, or <paramref name="func"/> is null and the item have to be created (the returned task is faulted)</exception>
        /// <remarks>
        /// Any exception thrown by <paramref name="func"/> is propagated to the caller (nothing is cached).
        /// Only one update per key runs at a time, other callers wait for it (without blocking a thread) or get default (see <paramref name="waitUntilReady"/>). <paramref name="func"/> may use the cache, but must not request the same key (dead lock).
        /// If <paramref name="waitUntilReady"/> is false and <paramref name="func"/> doesn't complete synchronously, the update continues in the background, callers that doesn't wait will get default until it completes.
        /// If an update fails, the caller that started it gets the exception, other callers waiting for it try again (one at a time), or get the exception if it was a background update.
        /// </remarks>
        public ValueTask<V> GetOrUpdateValueAsync<A>(K key, Func<K, A, ValueTask<V>> func, A arg, bool waitUntilReady = true)
        {
            if (C.TryGetValue(key, out var val))
            {
                if (DateTime.UtcNow < val.Item1)
                {
                    var task = val.Item3;
                    //  A running update is waited for (and retried if it fails) by the internal method
                    if ((!waitUntilReady) || (task == null) || task.IsCompletedSuccessfully)
                    {
                        Interlocked.Increment(ref HitCount);
                        return ValueTask.FromResult(((task != null) && task.IsCompletedSuccessfully) ? task.Result : val.Item2);
                    }
                }
            }
            return InternalGetOrUpdateAsync(key, func, arg, waitUntilReady);
        }



        /// <summary>
        /// Get an item from the cache, if it doesn't exist in the cache, the supplied delegate is executed to create the item.
        /// Only one item can be created at the same time (locked using the key), so no risk for "double" effort. 
        /// </summary>
        /// <typeparam name="A0">The type of the first custom argument</typeparam>
        /// <typeparam name="A1">The type of the second custom argument</typeparam>
        /// <param name="key">The key</param>
        /// <param name="func">The delegate used to create a non-existing item</param>
        /// <param name="arg0">A custom argument that is passed to the delegate if invoked</param>
        /// <param name="arg1">A custom argument that is passed to the delegate if invoked</param>
        /// <param name="waitUntilReady">If the item have to be updated, wait for the update before returning, else the default value will be returned and the update will be started concurrently</param>
        /// <returns>The value of the item or default if wait until ready is false and the update haven't completed yet</returns>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is null, or <paramref name="func"/> is null and the item have to be created (the returned task is faulted)</exception>
        /// <remarks>
        /// Any exception thrown by <paramref name="func"/> is propagated to the caller (nothing is cached).
        /// Only one update per key runs at a time, other callers wait for it (without blocking a thread) or get default (see <paramref name="waitUntilReady"/>). <paramref name="func"/> may use the cache, but must not request the same key (dead lock).
        /// If <paramref name="waitUntilReady"/> is false and <paramref name="func"/> doesn't complete synchronously, the update continues in the background, callers that doesn't wait will get default until it completes.
        /// If an update fails, the caller that started it gets the exception, other callers waiting for it try again (one at a time), or get the exception if it was a background update.
        /// </remarks>
        public ValueTask<V> GetOrUpdateValueAsync<A0, A1>(K key, Func<K, A0, A1, ValueTask<V>> func, A0 arg0, A1 arg1, bool waitUntilReady = true)
        {
            if (C.TryGetValue(key, out var val))
            {
                if (DateTime.UtcNow < val.Item1)
                {
                    var task = val.Item3;
                    //  A running update is waited for (and retried if it fails) by the internal method
                    if ((!waitUntilReady) || (task == null) || task.IsCompletedSuccessfully)
                    {
                        Interlocked.Increment(ref HitCount);
                        return ValueTask.FromResult(((task != null) && task.IsCompletedSuccessfully) ? task.Result : val.Item2);
                    }
                }
            }
            return InternalGetOrUpdateAsync(key, func, arg0, arg1, waitUntilReady);
        }

        #endregion//ValueTask

        #endregion//Without exising



        /// <summary>
        /// Call to manually prune (remove) old items, no real need to call this unless memory usage is the primary concern
        /// </summary>
        /// <remarks>
        /// Pruning is performed automatically after every write.
        /// With a fixed duration, items are pruned in insertion order (same as expiration order), pruning stops at the first item that hasn't expired yet.
        /// With an expiration function, items are pruned in expiration order (using a priority queue).
        /// Items that are locked (being updated) at the time of the prune are pruned later.
        /// </remarks>
        public void Prune()
        {
            var now = DateTime.UtcNow;
            if (H == null)
            {
                if (!Q.TryPeek(out var v))
                    return;
                if (now < v.Item1)
                    return;
            }
            else
            {
                if (now.Ticks < Volatile.Read(ref NextExpiration))
                    return;
            }
            InternalPrune(now);
        }

        /// <summary>
        /// Perform the actual prune, moved to it's own method to make it easier for the JIT to inline the Prune method (checking for pruning)
        /// </summary>
        /// <param name="exp">Items that expires at or before this time are removed</param>
        void InternalPrune(DateTime exp)
        {
            var q = Q;
            List<ValueTuple<DateTime, K>> busy = null;
            lock (q)
            {
                var h = H;
                if (h == null)
                {
                    for (; ; )
                    {
                        if (!q.TryPeek(out var v))
                            break;
                        if (exp < v.Item1)
                            break;
                        q.TryDequeue(out v);
                        if (!TryRemoveTracked(v))
                            (busy ??= new List<ValueTuple<DateTime, K>>()).Add(v);
                    }
                    //  Try again later
                    if (busy != null)
                    {
                        foreach (var v in busy)
                            q.Enqueue(v);
                    }
                    return;
                }
                for (; ; )
                {
                    if (!h.TryPeek(out var v, out var p))
                        break;
                    if (exp < p)
                        break;
                    h.Dequeue();
                    if (!TryRemoveTracked(v))
                        (busy ??= new List<ValueTuple<DateTime, K>>()).Add(v);
                }
                //  Try again a bit later (else every write would retry while the key is locked)
                if (busy != null)
                {
                    var retry = exp + BusyRetryDelay;
                    foreach (var v in busy)
                        h.Enqueue(v, retry);
                }
                UpdateNextExpiration(h);
            }
        }

        /// <summary>
        /// Remove a tracked item from the cache, if it haven't been updated since it was tracked (the expiration time is the same)
        /// </summary>
        /// <param name="v">The expiration time and key of the tracked item</param>
        /// <returns>False if the key is locked (being updated), true if it was handled (removed or already updated / removed)</returns>
        bool TryRemoveTracked(ValueTuple<DateTime, K> v)
        {
            var key = v.Item2;
            var locks = Locks;
            //  Never wait for a key lock here, the key could be locked by this thread (a value factory using the cache) or by a long running (async) value factory
            if (!locks.TryAdd(key, 0))
                return false;
            var c = C;
            if (c.TryGetValue(key, out var val))
            {
                if (val.Item1 == v.Item1)
                    c.TryRemove(key, out var _);
            }
            locks.TryRemove(key, out var _);
            return true;
        }

        /// <summary>
        /// Track an item for pruning (items that never expires are not tracked)
        /// </summary>
        /// <param name="exp">The expiration time of the item</param>
        /// <param name="key">The key of the item</param>
        void Track(DateTime exp, K key)
        {
            if (exp == DateTime.MaxValue)
                return;
            var h = H;
            if (h == null)
            {
                Q.Enqueue(ValueTuple.Create(exp, key));
                return;
            }
            lock (Q)
            {
                h.Enqueue(ValueTuple.Create(exp, key), exp);
                if (h.Count >= HeapLimit)
                {
                    //  The heap contains entries of items that have been updated / removed since, rebuild it from the cache to keep it's size bounded (amortized O(1) per write)
                    var items = new List<(ValueTuple<DateTime, K>, DateTime)>();
                    foreach (var kv in C)
                    {
                        var e = kv.Value;
                        if ((e.Item3 != null) || (e.Item1 == DateTime.MaxValue))
                            continue;
                        items.Add((ValueTuple.Create(e.Item1, kv.Key), e.Item1));
                    }
                    //  Writes that happens concurrently are tracked after this (we hold the lock), so nothing is lost (duplicates are harmless)
                    h.Clear();
                    h.EnqueueRange(items);
                    HeapLimit = Math.Max(MinHeapLimit, h.Count * 2);
                    UpdateNextExpiration(h);
                    return;
                }
                if (exp.Ticks < NextExpiration)
                    Volatile.Write(ref NextExpiration, exp.Ticks);
            }
        }

        /// <summary>
        /// Update the next expiration time (the lock must be held)
        /// </summary>
        /// <param name="h">The priority queue</param>
        void UpdateNextExpiration(PriorityQueue<ValueTuple<DateTime, K>, DateTime> h)
            => Volatile.Write(ref NextExpiration, h.TryPeek(out var _, out var p) ? p.Ticks : long.MaxValue);

        /// <summary>
        /// Remove items that never expires (they are not tracked for pruning), unless they are locked (being updated) or pending (the lock must be held)
        /// </summary>
        void ClearUntracked()
        {
            var c = C;
            var locks = Locks;
            foreach (var kv in c)
            {
                var e = kv.Value;
                if ((e.Item1 != DateTime.MaxValue) || (e.Item3 != null))
                    continue;
                var key = kv.Key;
                if (!locks.TryAdd(key, 0))
                    continue;
                c.TryRemove(kv);
                locks.TryRemove(key, out var _);
            }
        }


        /// <summary>
        /// Remove an entry from the cache
        /// </summary>
        /// <param name="key">The key of the item to remove</param>
        /// <returns>True if an item was removed (expired items that haven't been pruned yet are also removed), false if there was no item with the key</returns>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is null</exception>
        /// <remarks>Waits for any ongoing synchronous update of the key and removes it's value too, so a value created by an update that started before the remove isn't kept (must not be called for the same key from within a value factory, dead lock).
        /// An ongoing asynchronous update is represented by a pending entry, it's removed and the result of the update is not cached.</remarks>
        public bool Remove(K key)
        {
            var c = C;
            var removed = c.TryRemove(key, out var _);
            Lock(key);
            try
            {
                removed |= c.TryRemove(key, out _);
            }
            finally
            {
                Unlock(key);
            }
            return removed;
        }


        /// <summary>
        /// Clear cached values
        /// </summary>
        /// <remarks>
        /// Items that are being updated concurrently (or created in the background) may remain in the cache.
        /// The statistics are not reset, use <see cref="ResetStats"/> for that.
        /// </remarks>
        public void Clear()
        {
            var q = Q;
            List<ValueTuple<DateTime, K>> busy = null;
            lock (q)
            {
                var h = H;
                if (h == null)
                {
                    for (; ; )
                    {
                        if (!q.TryDequeue(out var v))
                            break;
                        if (!TryRemoveTracked(v))
                            (busy ??= new List<ValueTuple<DateTime, K>>()).Add(v);
                    }
                    //  Keys that are being updated are kept (the update will replace the value anyway), keep them in the queue so that they are pruned later
                    if (busy != null)
                    {
                        foreach (var v in busy)
                            q.Enqueue(v);
                    }
                }
                else
                {
                    while (h.TryDequeue(out var v, out var _))
                    {
                        if (!TryRemoveTracked(v))
                            (busy ??= new List<ValueTuple<DateTime, K>>()).Add(v);
                    }
                    //  Keys that are being updated are kept (the update will replace the value anyway), keep them in the queue so that they are pruned later
                    if (busy != null)
                    {
                        foreach (var v in busy)
                            h.Enqueue(v, v.Item1);
                    }
                    UpdateNextExpiration(h);
                }
                //  Items that never expires are not tracked
                ClearUntracked();
            }
        }

        /// <summary>
        /// Get some stats for the cache using Stats type
        /// </summary>
        /// <param name="system">A system name for the cache</param>
        /// <param name="prefix">An optional prefix to add to the stats name (null is treated as an empty string)</param>
        /// <returns>Stats: "Size", "Total count", "Hit ratio", "Semi hit ratio" and "Miss ratio" (ratios are in percent)</returns>
        public IEnumerable<Stats> GetStats(String system, String prefix = "")
        {
            prefix = prefix ?? "";
            var h = Interlocked.Read(ref HitCount);
            var s = Interlocked.Read(ref SemiHitCount);
            var m = Interlocked.Read(ref MissCount);
            var count = C.Count;
            var tot = h + s + m;
            var totOrg = tot;
            if (tot <= 0)
                tot = 1;
            yield return new Stats(system, prefix + "Size", count, "Number of items in the cache");
            yield return new Stats(system, prefix + "Total count", totOrg, "Number of times an item have been requested");
            yield return new Stats(system, prefix + "Hit ratio", (double)(((Decimal)h) * 100M / (Decimal)tot), "The ratio of cache hits (returns an existing item)", Data.TableDataNumberAttribute.Percentage);
            yield return new Stats(system, prefix + "Semi hit ratio", (double)(((Decimal)s) * 100M / (Decimal)tot), "The ratio of semi cache hits (returns an existing item, but had to take a lock to get it, so less optimal)", Data.TableDataNumberAttribute.Percentage);
            yield return new Stats(system, prefix + "Miss ratio", (double)(((Decimal)m) * 100M / (Decimal)tot), "The ratio of cache misses (doesn't have an item, and a new one have to be created)", Data.TableDataNumberAttribute.Percentage);
        }


        /// <summary>
        /// Get some stats about the cache performance
        /// </summary>
        /// <param name="hitRatio">The ratio [0, 1] of cache hits (GetOrUpdate returns an existing item)</param>
        /// <param name="semiHitRatio">The ratio [0, 1] of semi cache hits (GetOrUpdate returns an existing item, but had to take a lock to get it, so less optimal)</param>
        /// <param name="missRatio">The ratio [0, 1] of cache misses (GetOrUpdate doesn't have an item, and a new one have to be created)</param>
        /// <param name="hitCount">Number of cache hits (GetOrUpdate returns an existing item)</param>
        /// <param name="semiHitCount">Number of semi cache hits (GetOrUpdate returns an existing item, but had to take a lock to get it, so less optimal)</param>
        /// <param name="missCount">Number of cache misses (GetOrUpdate doesn't have an item, and a new one have to be created)</param>
        /// <param name="size">Number of items in the cache</param>
        /// <returns>The total number of GetOrUpdate requests</returns>
        public long GetStats(
            out double hitRatio, out double semiHitRatio, out double missRatio,
            out long hitCount, out long semiHitCount, out long missCount, out long size)
        {
            hitCount = Interlocked.Read(ref HitCount);
            semiHitCount = Interlocked.Read(ref SemiHitCount);
            missCount = Interlocked.Read(ref MissCount);
            size = C.Count;
            var tot = hitCount + semiHitCount + missCount;
            var totOrg = tot;
            if (tot <= 0)
                tot = 1;
            hitRatio = (double)(((Decimal)hitCount) / (Decimal)tot);
            semiHitRatio = (double)(((Decimal)semiHitCount) / (Decimal)tot);
            missRatio = (double)(((Decimal)missCount) / (Decimal)tot);
            return totOrg;
        }


        /// <summary>
        /// Get some stats about the cache performance
        /// </summary>
        /// <param name="hitCount">Number of cache hits (GetOrUpdate returns an existing item)</param>
        /// <param name="semiHitCount">Number of semi cache hits (GetOrUpdate returns an existing item, but had to take a lock to get it, so less optimal)</param>
        /// <param name="missCount">Number of cache misses (GetOrUpdate doesn't have an item, and a new one have to be created)</param>
        /// <param name="size">Number of items in the cache</param>
        public void GetStats(
            out long hitCount, out long semiHitCount, out long missCount, out long size)
        {
            hitCount = Interlocked.Read(ref HitCount);
            semiHitCount = Interlocked.Read(ref SemiHitCount);
            missCount = Interlocked.Read(ref MissCount);
            size = C.Count;
        }


        /// <summary>
        /// Reset all stats counters
        /// </summary>
        public void ResetStats()
        {
            Interlocked.Exchange(ref HitCount, 0);
            Interlocked.Exchange(ref SemiHitCount, 0);
            Interlocked.Exchange(ref MissCount, 0);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void Lock(K key)
        {
            if (!Locks.TryAdd(key, 0))
                LockSlow(key);
        }

        /// <summary>
        /// Spin until the key lock is acquired (same as SpinWait.SpinUntil, but without allocating a closure + delegate)
        /// </summary>
        /// <param name="key">The key to lock</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        void LockSlow(K key)
        {
            var locks = Locks;
            SpinWait spinner = default;
            do
            {
                spinner.SpinOnce();
            }
            while (!locks.TryAdd(key, 0));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void Unlock(K key)
        {
            Locks.TryRemove(key, out var _);
            Prune();
        }


        #region Update

        /// <summary>
        /// The core of all async updates.
        /// Only one update per key runs at a time: the key lock is only held while the cache entry is inspected and the update is started (never while awaiting),
        /// a running update is represented by a pending entry (expires never, value is default, Item3 is the task of the update).
        /// Callers with <paramref name="waitUntilReady"/> = false gets the value of the pending entry (default) while the update runs,
        /// callers with <paramref name="waitUntilReady"/> = true awaits the update (without blocking a thread).
        /// If an update fails, the caller that started it gets the exception, a failed waited update restores the previous (expired) entry, a failed background update removes the entry.
        /// Callers waiting for a failed update started by a waiting caller try again (one at a time, like when they waited for the key lock),
        /// callers waiting for a failed background update (started with <paramref name="waitUntilReady"/> = false) get the exception.
        /// </summary>
        /// <typeparam name="S">The type of the state passed to <paramref name="start"/></typeparam>
        /// <param name="key">The key</param>
        /// <param name="func">The value factory supplied by the user (only used for a null check)</param>
        /// <param name="state">The state passed to <paramref name="start"/></param>
        /// <param name="start">Starts the value factory: (key, existing (expired) value or default, state)</param>
        /// <param name="waitUntilReady">If true, wait for the update, else return default while the update runs</param>
        /// <returns>The value</returns>
        async ValueTask<V> CoreGetOrUpdateAsync<S>(K key, Delegate func, S state, Func<K, V, S, ValueTask<V>> start, bool waitUntilReady)
        {
            for (; ; )
            {
                Task<V> wait = null;
                Task<V> own = null;
                Lock(key);
                try
                {
                    var c = C;
                    var exists = c.TryGetValue(key, out var val);
                    if (exists && (DateTime.UtcNow < val.Item1))
                    {
                        //  Someone else added this cache entry (or an update is running)
                        Interlocked.Increment(ref SemiHitCount);
                        var task = val.Item3;
                        if (task == null)
                            return val.Item2;
                        if (task.IsCompletedSuccessfully)
                            return task.Result;
                        if (!waitUntilReady)
                            return val.Item2;
                        if (!task.IsCompleted)
                            wait = task;
                        //  else: the update failed (the entry is about to be restored), try again
                    }
                    else
                    {
                        Interlocked.Increment(ref MissCount);
                        ArgumentNullException.ThrowIfNull(func);
                        ValueTask<V> work;
                        V r;
                        try
                        {
                            work = start(key, exists ? val.Item2 : default, state);
                            //  Completed synchronously
                            r = work.IsCompleted ? work.GetAwaiter().GetResult() : default;
                        }
                        catch
                        {
                            //  A failed background update removes the (expired) entry, a failed waited update leaves it
                            if (!waitUntilReady)
                                c.TryRemove(key, out var _);
                            throw;
                        }
                        if (work.IsCompleted)
                        {
                            Store(key, r);
                            return r;
                        }
                        //  Write the pending entry before the update can complete, so that the update only replaces / restores its own pending entry
                        var tcs = waitUntilReady ? new TaskCompletionSource<V>(WaitedUpdate, TaskCreationOptions.RunContinuationsAsynchronously) : new TaskCompletionSource<V>(TaskCreationOptions.RunContinuationsAsynchronously);
                        var pending = ValueTuple.Create(DateTime.MaxValue, default(V), tcs.Task);
                        c[key] = pending;
                        _ = UpdateAsync(key, work, pending, exists ? val : default, exists && waitUntilReady, tcs);
                        if (!waitUntilReady)
                            return default;
                        own = tcs.Task;
                    }
                }
                finally
                {
                    Unlock(key);
                }
                //  Our own update, exceptions are propagated to the caller
                if (own != null)
                    return await own.ConfigureAwait(false);
                if (wait != null)
                {
                    try
                    {
                        return await wait.ConfigureAwait(false);
                    }
                    catch
                    {
                        //  A failed background update is reported to the waiting callers, a failed waited update is retried
                        if (!ReferenceEquals(wait.AsyncState, WaitedUpdate))
                            throw;
                    }
                }
            }
        }

        /// <summary>
        /// The state of the task of an update started by a waiting caller (callers waiting for it retry if it fails)
        /// </summary>
        static readonly Object WaitedUpdate = new Object();

        /// <summary>
        /// Store a new value in the cache (the key lock must be held)
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="value">The value</param>
        void Store(K key, V value)
        {
            var val = ValueTuple.Create(GetExpirationDate(value), value, (Task<V>)null);
            C[key] = val;
            Track(val.Item1, key);
        }

        /// <summary>
        /// Await the update and replace the pending entry with the result.
        /// If the update fails the previous (expired) entry is restored if <paramref name="hadPrevious"/> is true, else the pending entry is removed.
        /// If the pending entry have been replaced or removed (Set, Remove etc) while the update was running, the cache is left untouched.
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="work">The running update</param>
        /// <param name="pending">The pending entry (already written to the cache)</param>
        /// <param name="previous">The previous (expired) entry</param>
        /// <param name="hadPrevious">True if the previous entry should be restored if the update fails (waited updates only)</param>
        /// <param name="tcs">The completion source of the pending entry's task</param>
        async Task UpdateAsync(K key, ValueTask<V> work, ValueTuple<DateTime, V, Task<V>> pending, ValueTuple<DateTime, V, Task<V>> previous, bool hadPrevious, TaskCompletionSource<V> tcs)
        {
            V v;
            ValueTuple<DateTime, V, Task<V>> val;
            try
            {
                v = await work.ConfigureAwait(false);
                val = ValueTuple.Create(GetExpirationDate(v), v, (Task<V>)null);
            }
            catch (Exception ex)
            {
                if (hadPrevious)
                {
                    //  The previous entry may have been dropped from the prune queue (while the pending entry was cached), track it again
                    if (C.TryUpdate(key, previous, pending))
                        Track(previous.Item1, key);
                }
                else
                    C.TryRemove(new KeyValuePair<K, ValueTuple<DateTime, V, Task<V>>>(key, pending));
                tcs.SetException(ex);
                return;
            }
            if (C.TryUpdate(key, val, pending))
                Track(val.Item1, key);
            tcs.SetResult(v);
        }


        #region With existing

        #region Task

        ValueTask<V> InternalGetOrUpdateAsync<A>(K key, Func<K, V, A, Task<V>> func, A arg, bool waitUntilReady)
            => CoreGetOrUpdateAsync(key, func, (func, arg), static (k, v, s) => new ValueTask<V>(s.func(k, v, s.arg)), waitUntilReady);

        ValueTask<V> InternalGetOrUpdateAsync<A0, A1>(K key, Func<K, V, A0, A1, Task<V>> func, A0 arg0, A1 arg1, bool waitUntilReady)
            => CoreGetOrUpdateAsync(key, func, (func, arg0, arg1), static (k, v, s) => new ValueTask<V>(s.func(k, v, s.arg0, s.arg1)), waitUntilReady);

        ValueTask<V> InternalGetOrUpdateAsync(K key, Func<K, V, Task<V>> func, bool waitUntilReady)
            => CoreGetOrUpdateAsync(key, func, func, static (k, v, f) => new ValueTask<V>(f(k, v)), waitUntilReady);

        #endregion//Task

        #region ValueTask

        ValueTask<V> InternalGetOrUpdateAsync<A>(K key, Func<K, V, A, ValueTask<V>> func, A arg, bool waitUntilReady)
            => CoreGetOrUpdateAsync(key, func, (func, arg), static (k, v, s) => s.func(k, v, s.arg), waitUntilReady);

        ValueTask<V> InternalGetOrUpdateAsync<A0, A1>(K key, Func<K, V, A0, A1, ValueTask<V>> func, A0 arg0, A1 arg1, bool waitUntilReady)
            => CoreGetOrUpdateAsync(key, func, (func, arg0, arg1), static (k, v, s) => s.func(k, v, s.arg0, s.arg1), waitUntilReady);

        ValueTask<V> InternalGetOrUpdateAsync(K key, Func<K, V, ValueTask<V>> func, bool waitUntilReady)
            => CoreGetOrUpdateAsync(key, func, func, static (k, v, f) => f(k, v), waitUntilReady);

        #endregion//ValueTask

        #endregion//With existing

        #region Without existing

        #region Task

        ValueTask<V> InternalGetOrUpdateAsync<A>(K key, Func<K, A, Task<V>> func, A arg, bool waitUntilReady)
            => CoreGetOrUpdateAsync(key, func, (func, arg), static (k, _, s) => new ValueTask<V>(s.func(k, s.arg)), waitUntilReady);

        ValueTask<V> InternalGetOrUpdateAsync<A0, A1>(K key, Func<K, A0, A1, Task<V>> func, A0 arg0, A1 arg1, bool waitUntilReady)
            => CoreGetOrUpdateAsync(key, func, (func, arg0, arg1), static (k, _, s) => new ValueTask<V>(s.func(k, s.arg0, s.arg1)), waitUntilReady);

        ValueTask<V> InternalGetOrUpdateAsync(K key, Func<K, Task<V>> func, bool waitUntilReady)
            => CoreGetOrUpdateAsync(key, func, func, static (k, _, f) => new ValueTask<V>(f(k)), waitUntilReady);

        #endregion//Task

        #region ValueTask

        ValueTask<V> InternalGetOrUpdateAsync<A>(K key, Func<K, A, ValueTask<V>> func, A arg, bool waitUntilReady)
            => CoreGetOrUpdateAsync(key, func, (func, arg), static (k, _, s) => s.func(k, s.arg), waitUntilReady);

        ValueTask<V> InternalGetOrUpdateAsync<A0, A1>(K key, Func<K, A0, A1, ValueTask<V>> func, A0 arg0, A1 arg1, bool waitUntilReady)
            => CoreGetOrUpdateAsync(key, func, (func, arg0, arg1), static (k, _, s) => s.func(k, s.arg0, s.arg1), waitUntilReady);

        ValueTask<V> InternalGetOrUpdateAsync(K key, Func<K, ValueTask<V>> func, bool waitUntilReady)
            => CoreGetOrUpdateAsync(key, func, func, static (k, _, f) => f(k), waitUntilReady);

        #endregion//ValueTask

        #endregion//Without existing



        #endregion//Update



        long HitCount;
        long SemiHitCount;
        long MissCount;

        readonly ConcurrentDictionary<K, int> Locks;
        readonly ConcurrentDictionary<K, ValueTuple<DateTime, V, Task<V>>> C;
        /// <summary>
        /// The prune queue (expiration time and key in insertion order) when the expiration times are increasing (fixed duration), also used as the prune lock
        /// </summary>
        readonly ConcurrentQueue<ValueTuple<DateTime, K>> Q = new ();

        /// <summary>
        /// The prune queue (expiration time and key ordered by expiration time) when an expiration function is used (null if the expiration times are increasing).
        /// Access requires the lock (Q).
        /// </summary>
        readonly PriorityQueue<ValueTuple<DateTime, K>, DateTime> H;

        /// <summary>
        /// The ticks of the earliest prune time in <see cref="H"/> (long.MaxValue if empty), so that <see cref="Prune"/> can check without taking the lock
        /// </summary>
        long NextExpiration = long.MaxValue;

        /// <summary>
        /// When <see cref="H"/> reaches this size it's rebuilt from the cache (removing entries of items that have been updated or removed since they were tracked)
        /// </summary>
        int HeapLimit = MinHeapLimit;

        /// <summary>
        /// The minimum size of <see cref="H"/> before it's rebuilt
        /// </summary>
        const int MinHeapLimit = 1024;

        /// <summary>
        /// The time to wait before trying to prune an expired item that was locked (being updated) again
        /// </summary>
        static readonly TimeSpan BusyRetryDelay = TimeSpan.FromMilliseconds(100);

        readonly Func<V, DateTime> GetExpirationDate;

        /// <summary>
        /// Get the count of cached items (somewhat slow)
        /// </summary>
        /// <returns>Number of cached items (including expired items that haven't been pruned yet, and items being created in the background)</returns>
        public int GetCount() => C.Count;

        /// <summary>
        /// Enumerate all items in the cache (a moment in time snapshot is not guaranteed)
        /// </summary>
        /// <returns>An enumerator of (expiration time, key, value) tuples.
        /// Includes expired items that haven't been pruned yet, items that are being created (pending entries without a value) are skipped</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerator<(DateTime, K, V)> GetEnumerator() => C.Where(x => x.Value.Item3 == null).Select(x => (x.Value.Item1, x.Key, x.Value.Item2)).GetEnumerator();

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    }



}
