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
    /// Pruning walks a queue in insertion order and stops at the first item that hasn't expired, so it works best when the expiration times are (roughly) increasing,
    /// like with a fixed duration.
    /// Updates of the same key are serialized using a per key spin lock (a lock entry in a concurrent dictionary), so a value factory is only executed once for a key at the same time.
    /// Callers waiting for the key lock spin / sleep (they block a thread, even in the async methods).
    /// Keys can't be null.
    /// Async methods with waitUntilReady = false store a pending entry while the value is created in the background,
    /// the synchronous methods (GetOrUpdate etc) treat that entry as a cache hit and return default until the value is ready.
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
            : this(GetTimeoutFunc(timeout), comparer)
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
        /// Note that an item that expires later than items added after it delays the pruning of those items (see <see cref="Prune"/>).</param>
        /// <param name="comparer">An optional key comparer, null to use the default comparer</param>
        /// <exception cref="ArgumentNullException"><paramref name="getExpirationTimeUtc"/> is null</exception>
        public FastMemCache(Func<V, DateTime> getExpirationTimeUtc, IEqualityComparer<K> comparer = null)
        {
            ArgumentNullException.ThrowIfNull(getExpirationTimeUtc);
            GetExpirationDate = getExpirationTimeUtc;
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
                Q.Enqueue(ValueTuple.Create(val.Item1, key));
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
                Q.Enqueue(ValueTuple.Create(val.Item1, key));
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
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is null, or <paramref name="func"/> is null and the item have to be created</exception>
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
                ArgumentNullException.ThrowIfNull(func);
                var value = func(key, val.Item2);
                val = ValueTuple.Create(GetExpirationDate(value), value, (Task<V>)null);
                c[key] = val;
                Q.Enqueue(ValueTuple.Create(val.Item1, key));
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
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is null, or <paramref name="func"/> is null and the item have to be created</exception>
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
                ArgumentNullException.ThrowIfNull(func);
                var value = func(key, val.Item2, arg);
                val = ValueTuple.Create(GetExpirationDate(value), value, (Task<V>)null);
                c[key] = val;
                Q.Enqueue(ValueTuple.Create(val.Item1, key));
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
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is null, or <paramref name="func"/> is null and the item have to be created</exception>
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
                ArgumentNullException.ThrowIfNull(func);
                var value = func(key, val.Item2, arg0, arg1);
                val = ValueTuple.Create(GetExpirationDate(value), value, (Task<V>)null);
                c[key] = val;
                Q.Enqueue(ValueTuple.Create(val.Item1, key));
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
        /// The key is locked while <paramref name="func"/> executes, so <paramref name="func"/> may use the cache, but must not request the same key (dead lock).
        /// The existing value passed to <paramref name="func"/> is the expired value (if it still exist in the cache) or default.
        /// If <paramref name="waitUntilReady"/> is false and <paramref name="func"/> doesn't complete synchronously, the update continues in the background, callers that doesn't wait will get default until it completes.
        /// If a background update fails, the entry is removed (and the exception is thrown to any callers waiting for it).
        /// </remarks>
        public ValueTask<V> GetOrUpdateWithExistingAsync(K key, Func<K, V, Task<V>> func, bool waitUntilReady = true)
        {
            if (C.TryGetValue(key, out var val))
            {
                if (DateTime.UtcNow < val.Item1)
                {
                    Interlocked.Increment(ref HitCount);
                    if (waitUntilReady)
                    {
                        var task = val.Item3;
                        if ((task != null) && (!task.IsCompleted))
                            return WaitUntilReady(task);
                    }
                    return ValueTask.FromResult(val.Item2);
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
        /// The key is locked while <paramref name="func"/> executes, so <paramref name="func"/> may use the cache, but must not request the same key (dead lock).
        /// The existing value passed to <paramref name="func"/> is the expired value (if it still exist in the cache) or default.
        /// If <paramref name="waitUntilReady"/> is false and <paramref name="func"/> doesn't complete synchronously, the update continues in the background, callers that doesn't wait will get default until it completes.
        /// If a background update fails, the entry is removed (and the exception is thrown to any callers waiting for it).
        /// </remarks>
        public ValueTask<V> GetOrUpdateWithExistingAsync<A>(K key, Func<K, V, A, Task<V>> func, A arg, bool waitUntilReady = true)
        {
            if (C.TryGetValue(key, out var val))
            {
                if (DateTime.UtcNow < val.Item1)
                {
                    Interlocked.Increment(ref HitCount);
                    if (waitUntilReady)
                    {
                        var task = val.Item3;
                        if ((task != null) && (!task.IsCompleted))
                            return WaitUntilReady(task);
                    }
                    return ValueTask.FromResult(val.Item2);
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
        /// The key is locked while <paramref name="func"/> executes, so <paramref name="func"/> may use the cache, but must not request the same key (dead lock).
        /// The existing value passed to <paramref name="func"/> is the expired value (if it still exist in the cache) or default.
        /// If <paramref name="waitUntilReady"/> is false and <paramref name="func"/> doesn't complete synchronously, the update continues in the background, callers that doesn't wait will get default until it completes.
        /// If a background update fails, the entry is removed (and the exception is thrown to any callers waiting for it).
        /// </remarks>
        public ValueTask<V> GetOrUpdateWithExistingAsync<A0, A1>(K key, Func<K, V, A0, A1, Task<V>> func, A0 arg0, A1 arg1, bool waitUntilReady = true)
        {
            if (C.TryGetValue(key, out var val))
            {
                if (DateTime.UtcNow < val.Item1)
                {
                    Interlocked.Increment(ref HitCount);
                    if (waitUntilReady)
                    {
                        var task = val.Item3;
                        if ((task != null) && (!task.IsCompleted))
                            return WaitUntilReady(task);
                    }
                    return ValueTask.FromResult(val.Item2);
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
        /// The key is locked while <paramref name="func"/> executes, so <paramref name="func"/> may use the cache, but must not request the same key (dead lock).
        /// The existing value passed to <paramref name="func"/> is the expired value (if it still exist in the cache) or default.
        /// If <paramref name="waitUntilReady"/> is false and <paramref name="func"/> doesn't complete synchronously, the update continues in the background, callers that doesn't wait will get default until it completes.
        /// If a background update fails, the entry is removed (and the exception is thrown to any callers waiting for it).
        /// </remarks>
        public ValueTask<V> GetOrUpdateWithExistingValueAsync(K key, Func<K, V, ValueTask<V>> func, bool waitUntilReady = true)
        {
            if (C.TryGetValue(key, out var val))
            {
                if (DateTime.UtcNow < val.Item1)
                {
                    Interlocked.Increment(ref HitCount);
                    if (waitUntilReady)
                    {
                        var task = val.Item3;
                        if ((task != null) && (!task.IsCompleted))
                            return WaitUntilReady(task);
                    }
                    return ValueTask.FromResult(val.Item2);
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
        /// The key is locked while <paramref name="func"/> executes, so <paramref name="func"/> may use the cache, but must not request the same key (dead lock).
        /// The existing value passed to <paramref name="func"/> is the expired value (if it still exist in the cache) or default.
        /// If <paramref name="waitUntilReady"/> is false and <paramref name="func"/> doesn't complete synchronously, the update continues in the background, callers that doesn't wait will get default until it completes.
        /// If a background update fails, the entry is removed (and the exception is thrown to any callers waiting for it).
        /// </remarks>
        public ValueTask<V> GetOrUpdateWithExistingValueAsync<A>(K key, Func<K, V, A, ValueTask<V>> func, A arg, bool waitUntilReady = true)
        {
            if (C.TryGetValue(key, out var val))
            {
                if (DateTime.UtcNow < val.Item1)
                {
                    Interlocked.Increment(ref HitCount);
                    if (waitUntilReady)
                    {
                        var task = val.Item3;
                        if ((task != null) && (!task.IsCompleted))
                            return WaitUntilReady(task);
                    }
                    return ValueTask.FromResult(val.Item2);
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
        /// The key is locked while <paramref name="func"/> executes, so <paramref name="func"/> may use the cache, but must not request the same key (dead lock).
        /// The existing value passed to <paramref name="func"/> is the expired value (if it still exist in the cache) or default.
        /// If <paramref name="waitUntilReady"/> is false and <paramref name="func"/> doesn't complete synchronously, the update continues in the background, callers that doesn't wait will get default until it completes.
        /// If a background update fails, the entry is removed (and the exception is thrown to any callers waiting for it).
        /// </remarks>
        public ValueTask<V> GetOrUpdateWithExistingValueAsync<A0, A1>(K key, Func<K, V, A0, A1, ValueTask<V>> func, A0 arg0, A1 arg1, bool waitUntilReady = true)
        {
            if (C.TryGetValue(key, out var val))
            {
                if (DateTime.UtcNow < val.Item1)
                {
                    Interlocked.Increment(ref HitCount);
                    if (waitUntilReady)
                    {
                        var task = val.Item3;
                        if ((task != null) && (!task.IsCompleted))
                            return WaitUntilReady(task);
                    }
                    return ValueTask.FromResult(val.Item2);
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
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is null, or <paramref name="func"/> is null and the item have to be created</exception>
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
                ArgumentNullException.ThrowIfNull(func);
                var value = func(key);
                val = ValueTuple.Create(GetExpirationDate(value), value, (Task<V>)null);
                c[key] = val;
                Q.Enqueue(ValueTuple.Create(val.Item1, key));
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
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is null, or <paramref name="func"/> is null and the item have to be created</exception>
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
                ArgumentNullException.ThrowIfNull(func);
                var value = func(key, arg);
                val = ValueTuple.Create(GetExpirationDate(value), value, (Task<V>)null);
                c[key] = val;
                Q.Enqueue(ValueTuple.Create(val.Item1, key));
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
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is null, or <paramref name="func"/> is null and the item have to be created</exception>
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
                ArgumentNullException.ThrowIfNull(func);
                var value = func(key, arg0, arg1);
                val = ValueTuple.Create(GetExpirationDate(value), value, (Task<V>)null);
                c[key] = val;
                Q.Enqueue(ValueTuple.Create(val.Item1, key));
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
        /// The key is locked while <paramref name="func"/> executes, so <paramref name="func"/> may use the cache, but must not request the same key (dead lock).
        /// If <paramref name="waitUntilReady"/> is false and <paramref name="func"/> doesn't complete synchronously, the update continues in the background, callers that doesn't wait will get default until it completes.
        /// If a background update fails, the entry is removed (and the exception is thrown to any callers waiting for it).
        /// </remarks>
        public ValueTask<V> GetOrUpdateAsync(K key, Func<K, Task<V>> func, bool waitUntilReady = true)
        {
            if (C.TryGetValue(key, out var val))
            {
                if (DateTime.UtcNow < val.Item1)
                {
                    Interlocked.Increment(ref HitCount);
                    if (waitUntilReady)
                    {
                        var task = val.Item3;
                        if ((task != null) && (!task.IsCompleted))
                            return WaitUntilReady(task);
                    }
                    return ValueTask.FromResult(val.Item2);
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
        /// The key is locked while <paramref name="func"/> executes, so <paramref name="func"/> may use the cache, but must not request the same key (dead lock).
        /// If <paramref name="waitUntilReady"/> is false and <paramref name="func"/> doesn't complete synchronously, the update continues in the background, callers that doesn't wait will get default until it completes.
        /// If a background update fails, the entry is removed (and the exception is thrown to any callers waiting for it).
        /// </remarks>
        public ValueTask<V> GetOrUpdateAsync<A>(K key, Func<K, A, Task<V>> func, A arg, bool waitUntilReady = true)
        {
            if (C.TryGetValue(key, out var val))
            {
                if (DateTime.UtcNow < val.Item1)
                {
                    Interlocked.Increment(ref HitCount);
                    if (waitUntilReady)
                    {
                        var task = val.Item3;
                        if ((task != null) && (!task.IsCompleted))
                            return WaitUntilReady(task);
                    }
                    return ValueTask.FromResult(val.Item2);
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
        /// The key is locked while <paramref name="func"/> executes, so <paramref name="func"/> may use the cache, but must not request the same key (dead lock).
        /// If <paramref name="waitUntilReady"/> is false and <paramref name="func"/> doesn't complete synchronously, the update continues in the background, callers that doesn't wait will get default until it completes.
        /// If a background update fails, the entry is removed (and the exception is thrown to any callers waiting for it).
        /// </remarks>
        public ValueTask<V> GetOrUpdateAsync<A0, A1>(K key, Func<K, A0, A1, Task<V>> func, A0 arg0, A1 arg1, bool waitUntilReady = true)
        {
            if (C.TryGetValue(key, out var val))
            {
                if (DateTime.UtcNow < val.Item1)
                {
                    Interlocked.Increment(ref HitCount);
                    if (waitUntilReady)
                    {
                        var task = val.Item3;
                        if ((task != null) && (!task.IsCompleted))
                            return WaitUntilReady(task);
                    }
                    return ValueTask.FromResult(val.Item2);
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
        /// The key is locked while <paramref name="func"/> executes, so <paramref name="func"/> may use the cache, but must not request the same key (dead lock).
        /// If <paramref name="waitUntilReady"/> is false and <paramref name="func"/> doesn't complete synchronously, the update continues in the background, callers that doesn't wait will get default until it completes.
        /// If a background update fails, the entry is removed (and the exception is thrown to any callers waiting for it).
        /// </remarks>
        public ValueTask<V> GetOrUpdateValueAsync(K key, Func<K, ValueTask<V>> func, bool waitUntilReady = true)
        {
            if (C.TryGetValue(key, out var val))
            {
                if (DateTime.UtcNow < val.Item1)
                {
                    Interlocked.Increment(ref HitCount);
                    if (waitUntilReady)
                    {
                        var task = val.Item3;
                        if ((task != null) && (!task.IsCompleted))
                            return WaitUntilReady(task);
                    }
                    return ValueTask.FromResult(val.Item2);
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
        /// The key is locked while <paramref name="func"/> executes, so <paramref name="func"/> may use the cache, but must not request the same key (dead lock).
        /// If <paramref name="waitUntilReady"/> is false and <paramref name="func"/> doesn't complete synchronously, the update continues in the background, callers that doesn't wait will get default until it completes.
        /// If a background update fails, the entry is removed (and the exception is thrown to any callers waiting for it).
        /// </remarks>
        public ValueTask<V> GetOrUpdateValueAsync<A>(K key, Func<K, A, ValueTask<V>> func, A arg, bool waitUntilReady = true)
        {
            if (C.TryGetValue(key, out var val))
            {
                if (DateTime.UtcNow < val.Item1)
                {
                    Interlocked.Increment(ref HitCount);
                    if (waitUntilReady)
                    {
                        var task = val.Item3;
                        if ((task != null) && (!task.IsCompleted))
                            return WaitUntilReady(task);
                    }
                    return ValueTask.FromResult(val.Item2);
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
        /// The key is locked while <paramref name="func"/> executes, so <paramref name="func"/> may use the cache, but must not request the same key (dead lock).
        /// If <paramref name="waitUntilReady"/> is false and <paramref name="func"/> doesn't complete synchronously, the update continues in the background, callers that doesn't wait will get default until it completes.
        /// If a background update fails, the entry is removed (and the exception is thrown to any callers waiting for it).
        /// </remarks>
        public ValueTask<V> GetOrUpdateValueAsync<A0, A1>(K key, Func<K, A0, A1, ValueTask<V>> func, A0 arg0, A1 arg1, bool waitUntilReady = true)
        {
            if (C.TryGetValue(key, out var val))
            {
                if (DateTime.UtcNow < val.Item1)
                {
                    Interlocked.Increment(ref HitCount);
                    if (waitUntilReady)
                    {
                        var task = val.Item3;
                        if ((task != null) && (!task.IsCompleted))
                            return WaitUntilReady(task);
                    }
                    return ValueTask.FromResult(val.Item2);
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
        /// Items are pruned in insertion order, pruning stops at the first item (in insertion order) that hasn't expired yet.
        /// Items that are locked (being updated) at the time of the prune are pruned later.
        /// </remarks>
        public void Prune()
        {
            var q = Q;
            if (!q.TryPeek(out var v))
                return;
            var exp = DateTime.UtcNow;
            if (exp < v.Item1)
                return;
            InternalPrune(exp);
        }

        /// <summary>
        /// Perform the actual prune, moved to it's own method to make it easier for the JIT to inline the Prune method (checking for pruning)
        /// </summary>
        /// <param name="exp">Items that expires at or before this time are removed</param>
        void InternalPrune(DateTime exp)
        {
            var q = Q;
            var c = C;
            var locks = Locks;
            List<ValueTuple<DateTime, K>> busy = null;
            lock (q)
            {
                for (; ; )
                {
                    if (!q.TryPeek(out var v))
                        break;
                    if (exp < v.Item1)
                        break;
                    q.TryDequeue(out v);
                    var key = v.Item2;
                    //  Never wait for a key lock here, the key could be locked by this thread (a value factory using the cache) or by a long running (async) value factory
                    if (!locks.TryAdd(key, 0))
                    {
                        (busy ??= new List<ValueTuple<DateTime, K>>()).Add(v);
                        continue;
                    }
                    if (c.TryGetValue(key, out var val))
                    {
                        if (val.Item1 == v.Item1)
                            c.TryRemove(key, out var _);
                    }
                    locks.TryRemove(key, out var _);
                }
                //  Try again later
                if (busy != null)
                {
                    foreach (var v in busy)
                        q.Enqueue(v);
                }
            }
        }


        /// <summary>
        /// Remove an entry from the cache
        /// </summary>
        /// <param name="key">The key of the item to remove</param>
        /// <returns>True if an item was removed (expired items that haven't been pruned yet are also removed), false if there was no item with the key</returns>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is null</exception>
        /// <remarks>If an item exists, waits for any ongoing update of the key (must not be called for the same key from within a value factory, dead lock).
        /// If no item exists, false is returned immediately (an ongoing update will add its value afterwards).</remarks>
        public bool Remove(K key)
        {
            var c = C;
            if (!c.TryRemove(key, out var _))
                return false;
            Lock(key);
            try
            {
                c.TryRemove(key, out _);
            }
            finally
            {
                Unlock(key);
            }
            return true;
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
            var c = C;
            var locks = Locks;
            List<ValueTuple<DateTime, K>> busy = null;
            lock (q)
            {
                for (; ; )
                {
                    if (!q.TryDequeue(out var v))
                        break;
                    var key = v.Item2;
                    //  Never wait for a key lock here, the key could be locked by this thread (a value factory using the cache) or by a long running (async) value factory
                    if (!locks.TryAdd(key, 0))
                    {
                        (busy ??= new List<ValueTuple<DateTime, K>>()).Add(v);
                        continue;
                    }
                    if (c.TryGetValue(key, out var val))
                    {
                        if (val.Item1 == v.Item1)
                            c.TryRemove(key, out var _);
                    }
                    locks.TryRemove(key, out var _);
                }
                //  Keys that are being updated are kept (the update will replace the value anyway), keep them in the queue so that they are pruned later
                if (busy != null)
                {
                    foreach (var v in busy)
                        q.Enqueue(v);
                }
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

        static async ValueTask<V> WaitUntilReady(Task<V> task)
            => await task.ConfigureAwait(false);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ValueTask<V> AsValueTask(Task<V> task) => new ValueTask<V>(task);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ValueTask<V> AsValueTask(ValueTask<V> task) => task;

        /// <summary>
        /// Store a new value in the cache (the key lock must be held, or the value must be the result of a build started while holding the lock)
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="value">The value</param>
        void Store(K key, V value)
        {
            var val = ValueTuple.Create(GetExpirationDate(value), value, (Task<V>)null);
            C[key] = val;
            Q.Enqueue(ValueTuple.Create(val.Item1, key));
        }

        /// <summary>
        /// Await the work and store the result in the cache, if the work fails the entry is removed
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="work">The pending value</param>
        /// <returns>The value</returns>
        async Task<V> BuildAsync(K key, ValueTask<V> work)
        {
            V v;
            try
            {
                v = await work.ConfigureAwait(false);
            }
            catch
            {
                C.TryRemove(key, out var _);
                throw;
            }
            Store(key, v);
            return v;
        }

        /// <summary>
        /// Start a background build (waitUntilReady = false), must be called while holding the key lock.
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="work">The pending value (as returned by the value factory)</param>
        /// <returns>The value if it completed synchronously, else default</returns>
        V StartBuild(K key, ValueTask<V> work)
        {
            if (work.IsCompletedSuccessfully)
            {
                var r = work.Result;
                Store(key, r);
                return r;
            }
            var task = BuildAsync(key, work);
            if (task.IsCompleted)
                return task.GetAwaiter().GetResult();
            var c = C;
            var pending = ValueTuple.Create(DateTime.MaxValue, default(V), task);
            c[key] = pending;
            if (task.IsCompleted)
            {
                //  The build completed before the pending entry was written, so the pending entry may have overwritten the result (or the removal of a failed build).
                //  Replace / remove the pending entry (unless the build already did it).
                if (task.IsCompletedSuccessfully)
                {
                    var r = task.Result;
                    var val = ValueTuple.Create(GetExpirationDate(r), r, (Task<V>)null);
                    if (c.TryUpdate(key, val, pending))
                        Q.Enqueue(ValueTuple.Create(val.Item1, key));
                }
                else
                {
                    c.TryRemove(new KeyValuePair<K, ValueTuple<DateTime, V, Task<V>>>(key, pending));
                }
            }
            return default;
        }


        #region With existing

        #region Task

        async ValueTask<V> InternalGetOrUpdateAsync<A>(K key, Func<K, V, A, Task<V>> func, A arg, bool waitUntilReady)
        {
            Lock(key);
            try
            {
                var c = C;
                //  Test if someone else added this cache entry
                if (c.TryGetValue(key, out var val))
                {
                    if (DateTime.UtcNow < val.Item1)
                    {
                        Interlocked.Increment(ref SemiHitCount);
                        if (waitUntilReady)
                        {
                            var task = val.Item3;
                            if ((task != null) && (!task.IsCompleted))
                                return await task.ConfigureAwait(false);
                        }
                        return val.Item2;
                    }
                }
                Interlocked.Increment(ref MissCount);
                ArgumentNullException.ThrowIfNull(func);
                if (waitUntilReady)
                {
                    var value = await func(key, val.Item2, arg).ConfigureAwait(false);
                    val = ValueTuple.Create(GetExpirationDate(value), value, (Task<V>)null);
                    c[key] = val;
                    Q.Enqueue(ValueTuple.Create(val.Item1, key));
                    return val.Item2;
                }
                else
                {
                    ValueTask<V> work;
                    try
                    {
                        work = AsValueTask(func(key, val.Item2, arg));
                    }
                    catch
                    {
                        c.TryRemove(key, out var _);
                        throw;
                    }
                    return StartBuild(key, work);
                }
            }
            finally
            {
                Unlock(key);
            }
        }

        async ValueTask<V> InternalGetOrUpdateAsync<A0, A1>(K key, Func<K, V, A0, A1, Task<V>> func, A0 arg0, A1 arg1, bool waitUntilReady)
        {
            Lock(key);
            try
            {
                var c = C;
                //  Test if someone else added this cache entry
                if (c.TryGetValue(key, out var val))
                {
                    if (DateTime.UtcNow < val.Item1)
                    {
                        Interlocked.Increment(ref SemiHitCount);
                        if (waitUntilReady)
                        {
                            var task = val.Item3;
                            if ((task != null) && (!task.IsCompleted))
                                return await task.ConfigureAwait(false);
                        }
                        return val.Item2;
                    }
                }
                Interlocked.Increment(ref MissCount);
                ArgumentNullException.ThrowIfNull(func);
                if (waitUntilReady)
                {
                    var value = await func(key, val.Item2, arg0, arg1).ConfigureAwait(false);
                    val = ValueTuple.Create(GetExpirationDate(value), value, (Task<V>)null);
                    c[key] = val;
                    Q.Enqueue(ValueTuple.Create(val.Item1, key));
                    return val.Item2;
                }
                else
                {
                    ValueTask<V> work;
                    try
                    {
                        work = AsValueTask(func(key, val.Item2, arg0, arg1));
                    }
                    catch
                    {
                        c.TryRemove(key, out var _);
                        throw;
                    }
                    return StartBuild(key, work);
                }
            }
            finally
            {
                Unlock(key);
            }
        }

        async ValueTask<V> InternalGetOrUpdateAsync(K key, Func<K, V, Task<V>> func, bool waitUntilReady)
        {
            Lock(key);
            try
            {
                var c = C;
                //  Test if someone else added this cache entry
                if (c.TryGetValue(key, out var val))
                {
                    if (DateTime.UtcNow < val.Item1)
                    {
                        Interlocked.Increment(ref SemiHitCount);
                        if (waitUntilReady)
                        {
                            var task = val.Item3;
                            if ((task != null) && (!task.IsCompleted))
                                return await task.ConfigureAwait(false);
                        }
                        return val.Item2;
                    }
                }
                Interlocked.Increment(ref MissCount);
                ArgumentNullException.ThrowIfNull(func);
                if (waitUntilReady)
                {
                    var value = await func(key, val.Item2).ConfigureAwait(false);
                    val = ValueTuple.Create(GetExpirationDate(value), value, (Task<V>)null);
                    c[key] = val;
                    Q.Enqueue(ValueTuple.Create(val.Item1, key));
                    return val.Item2;
                }
                else
                {
                    ValueTask<V> work;
                    try
                    {
                        work = AsValueTask(func(key, val.Item2));
                    }
                    catch
                    {
                        c.TryRemove(key, out var _);
                        throw;
                    }
                    return StartBuild(key, work);
                }
            }
            finally
            {
                Unlock(key);
            }
        }

        #endregion//Task



        #region ValueTask

        async ValueTask<V> InternalGetOrUpdateAsync<A>(K key, Func<K, V, A, ValueTask<V>> func, A arg, bool waitUntilReady)
        {
            Lock(key);
            try
            {
                var c = C;
                //  Test if someone else added this cache entry
                if (c.TryGetValue(key, out var val))
                {
                    if (DateTime.UtcNow < val.Item1)
                    {
                        Interlocked.Increment(ref SemiHitCount);
                        if (waitUntilReady)
                        {
                            var task = val.Item3;
                            if ((task != null) && (!task.IsCompleted))
                                return await task.ConfigureAwait(false);
                        }
                        return val.Item2;
                    }
                }
                Interlocked.Increment(ref MissCount);
                ArgumentNullException.ThrowIfNull(func);
                if (waitUntilReady)
                {
                    var value = await func(key, val.Item2, arg).ConfigureAwait(false);
                    val = ValueTuple.Create(GetExpirationDate(value), value, (Task<V>)null);
                    c[key] = val;
                    Q.Enqueue(ValueTuple.Create(val.Item1, key));
                    return val.Item2;
                }
                else
                {
                    ValueTask<V> work;
                    try
                    {
                        work = AsValueTask(func(key, val.Item2, arg));
                    }
                    catch
                    {
                        c.TryRemove(key, out var _);
                        throw;
                    }
                    return StartBuild(key, work);
                }
            }
            finally
            {
                Unlock(key);
            }
        }

        async ValueTask<V> InternalGetOrUpdateAsync<A0, A1>(K key, Func<K, V, A0, A1, ValueTask<V>> func, A0 arg0, A1 arg1, bool waitUntilReady)
        {
            Lock(key);
            try
            {
                var c = C;
                //  Test if someone else added this cache entry
                if (c.TryGetValue(key, out var val))
                {
                    if (DateTime.UtcNow < val.Item1)
                    {
                        Interlocked.Increment(ref SemiHitCount);
                        if (waitUntilReady)
                        {
                            var task = val.Item3;
                            if ((task != null) && (!task.IsCompleted))
                                return await task.ConfigureAwait(false);
                        }
                        return val.Item2;
                    }
                }
                Interlocked.Increment(ref MissCount);
                ArgumentNullException.ThrowIfNull(func);
                if (waitUntilReady)
                {
                    var value = await func(key, val.Item2, arg0, arg1).ConfigureAwait(false);
                    val = ValueTuple.Create(GetExpirationDate(value), value, (Task<V>)null);
                    c[key] = val;
                    Q.Enqueue(ValueTuple.Create(val.Item1, key));
                    return val.Item2;
                }
                else
                {
                    ValueTask<V> work;
                    try
                    {
                        work = AsValueTask(func(key, val.Item2, arg0, arg1));
                    }
                    catch
                    {
                        c.TryRemove(key, out var _);
                        throw;
                    }
                    return StartBuild(key, work);
                }
            }
            finally
            {
                Unlock(key);
            }
        }

        async ValueTask<V> InternalGetOrUpdateAsync(K key, Func<K, V, ValueTask<V>> func, bool waitUntilReady)
        {
            Lock(key);
            try
            {
                var c = C;
                //  Test if someone else added this cache entry
                if (c.TryGetValue(key, out var val))
                {
                    if (DateTime.UtcNow < val.Item1)
                    {
                        Interlocked.Increment(ref SemiHitCount);
                        if (waitUntilReady)
                        {
                            var task = val.Item3;
                            if ((task != null) && (!task.IsCompleted))
                                return await task.ConfigureAwait(false);
                        }
                        return val.Item2;
                    }
                }
                Interlocked.Increment(ref MissCount);
                ArgumentNullException.ThrowIfNull(func);
                if (waitUntilReady)
                {
                    var value = await func(key, val.Item2).ConfigureAwait(false);
                    val = ValueTuple.Create(GetExpirationDate(value), value, (Task<V>)null);
                    c[key] = val;
                    Q.Enqueue(ValueTuple.Create(val.Item1, key));
                    return val.Item2;
                }
                else
                {
                    ValueTask<V> work;
                    try
                    {
                        work = AsValueTask(func(key, val.Item2));
                    }
                    catch
                    {
                        c.TryRemove(key, out var _);
                        throw;
                    }
                    return StartBuild(key, work);
                }
            }
            finally
            {
                Unlock(key);
            }
        }

        #endregion//ValueTask

        #endregion//With existing



        #region Without existing

        #region Task

        async ValueTask<V> InternalGetOrUpdateAsync<A>(K key, Func<K, A, Task<V>> func, A arg, bool waitUntilReady)
        {
            Lock(key);
            try
            {
                var c = C;
                //  Test if someone else added this cache entry
                if (c.TryGetValue(key, out var val))
                {
                    if (DateTime.UtcNow < val.Item1)
                    {
                        Interlocked.Increment(ref SemiHitCount);
                        if (waitUntilReady)
                        {
                            var task = val.Item3;
                            if ((task != null) && (!task.IsCompleted))
                                return await task.ConfigureAwait(false);
                        }
                        return val.Item2;
                    }
                }
                Interlocked.Increment(ref MissCount);
                ArgumentNullException.ThrowIfNull(func);
                if (waitUntilReady)
                {
                    var value = await func(key, arg).ConfigureAwait(false);
                    val = ValueTuple.Create(GetExpirationDate(value), value, (Task<V>)null);
                    c[key] = val;
                    Q.Enqueue(ValueTuple.Create(val.Item1, key));
                    return val.Item2;
                }
                else
                {
                    ValueTask<V> work;
                    try
                    {
                        work = AsValueTask(func(key, arg));
                    }
                    catch
                    {
                        c.TryRemove(key, out var _);
                        throw;
                    }
                    return StartBuild(key, work);
                }
            }
            finally
            {
                Unlock(key);
            }
        }

        async ValueTask<V> InternalGetOrUpdateAsync<A0, A1>(K key, Func<K, A0, A1, Task<V>> func, A0 arg0, A1 arg1, bool waitUntilReady)
        {
            Lock(key);
            try
            {
                var c = C;
                //  Test if someone else added this cache entry
                if (c.TryGetValue(key, out var val))
                {
                    if (DateTime.UtcNow < val.Item1)
                    {
                        Interlocked.Increment(ref SemiHitCount);
                        if (waitUntilReady)
                        {
                            var task = val.Item3;
                            if ((task != null) && (!task.IsCompleted))
                                return await task.ConfigureAwait(false);
                        }
                        return val.Item2;
                    }
                }
                Interlocked.Increment(ref MissCount);
                ArgumentNullException.ThrowIfNull(func);
                if (waitUntilReady)
                {
                    var value = await func(key, arg0, arg1).ConfigureAwait(false);
                    val = ValueTuple.Create(GetExpirationDate(value), value, (Task<V>)null);
                    c[key] = val;
                    Q.Enqueue(ValueTuple.Create(val.Item1, key));
                    return val.Item2;
                }
                else
                {
                    ValueTask<V> work;
                    try
                    {
                        work = AsValueTask(func(key, arg0, arg1));
                    }
                    catch
                    {
                        c.TryRemove(key, out var _);
                        throw;
                    }
                    return StartBuild(key, work);
                }
            }
            finally
            {
                Unlock(key);
            }
        }

        async ValueTask<V> InternalGetOrUpdateAsync(K key, Func<K, Task<V>> func, bool waitUntilReady)
        {
            Lock(key);
            try
            {
                var c = C;
                //  Test if someone else added this cache entry
                if (c.TryGetValue(key, out var val))
                {
                    if (DateTime.UtcNow < val.Item1)
                    {
                        Interlocked.Increment(ref SemiHitCount);
                        if (waitUntilReady)
                        {
                            var task = val.Item3;
                            if ((task != null) && (!task.IsCompleted))
                                return await task.ConfigureAwait(false);
                        }
                        return val.Item2;
                    }
                }
                Interlocked.Increment(ref MissCount);
                ArgumentNullException.ThrowIfNull(func);
                if (waitUntilReady)
                {
                    var value = await func(key).ConfigureAwait(false);
                    val = ValueTuple.Create(GetExpirationDate(value), value, (Task<V>)null);
                    c[key] = val;
                    Q.Enqueue(ValueTuple.Create(val.Item1, key));
                    return val.Item2;
                }
                else
                {
                    ValueTask<V> work;
                    try
                    {
                        work = AsValueTask(func(key));
                    }
                    catch
                    {
                        c.TryRemove(key, out var _);
                        throw;
                    }
                    return StartBuild(key, work);
                }
            }
            finally
            {
                Unlock(key);
            }
        }

        #endregion//Task





        #region ValueTask

        async ValueTask<V> InternalGetOrUpdateAsync<A>(K key, Func<K, A, ValueTask<V>> func, A arg, bool waitUntilReady)
        {
            Lock(key);
            try
            {
                var c = C;
                //  Test if someone else added this cache entry
                if (c.TryGetValue(key, out var val))
                {
                    if (DateTime.UtcNow < val.Item1)
                    {
                        Interlocked.Increment(ref SemiHitCount);
                        if (waitUntilReady)
                        {
                            var task = val.Item3;
                            if ((task != null) && (!task.IsCompleted))
                                return await task.ConfigureAwait(false);
                        }
                        return val.Item2;
                    }
                }
                Interlocked.Increment(ref MissCount);
                ArgumentNullException.ThrowIfNull(func);
                if (waitUntilReady)
                {
                    var value = await func(key, arg).ConfigureAwait(false);
                    val = ValueTuple.Create(GetExpirationDate(value), value, (Task<V>)null);
                    c[key] = val;
                    Q.Enqueue(ValueTuple.Create(val.Item1, key));
                    return val.Item2;
                }
                else
                {
                    ValueTask<V> work;
                    try
                    {
                        work = AsValueTask(func(key, arg));
                    }
                    catch
                    {
                        c.TryRemove(key, out var _);
                        throw;
                    }
                    return StartBuild(key, work);
                }
            }
            finally
            {
                Unlock(key);
            }
        }

        async ValueTask<V> InternalGetOrUpdateAsync<A0, A1>(K key, Func<K, A0, A1, ValueTask<V>> func, A0 arg0, A1 arg1, bool waitUntilReady)
        {
            Lock(key);
            try
            {
                var c = C;
                //  Test if someone else added this cache entry
                if (c.TryGetValue(key, out var val))
                {
                    if (DateTime.UtcNow < val.Item1)
                    {
                        Interlocked.Increment(ref SemiHitCount);
                        if (waitUntilReady)
                        {
                            var task = val.Item3;
                            if ((task != null) && (!task.IsCompleted))
                                return await task.ConfigureAwait(false);
                        }
                        return val.Item2;
                    }
                }
                Interlocked.Increment(ref MissCount);
                ArgumentNullException.ThrowIfNull(func);
                if (waitUntilReady)
                {
                    var value = await func(key, arg0, arg1).ConfigureAwait(false);
                    val = ValueTuple.Create(GetExpirationDate(value), value, (Task<V>)null);
                    c[key] = val;
                    Q.Enqueue(ValueTuple.Create(val.Item1, key));
                    return val.Item2;
                }
                else
                {
                    ValueTask<V> work;
                    try
                    {
                        work = AsValueTask(func(key, arg0, arg1));
                    }
                    catch
                    {
                        c.TryRemove(key, out var _);
                        throw;
                    }
                    return StartBuild(key, work);
                }
            }
            finally
            {
                Unlock(key);
            }
        }

        async ValueTask<V> InternalGetOrUpdateAsync(K key, Func<K, ValueTask<V>> func, bool waitUntilReady)
        {
            Lock(key);
            try
            {
                var c = C;
                //  Test if someone else added this cache entry
                if (c.TryGetValue(key, out var val))
                {
                    if (DateTime.UtcNow < val.Item1)
                    {
                        Interlocked.Increment(ref SemiHitCount);
                        if (waitUntilReady)
                        {
                            var task = val.Item3;
                            if ((task != null) && (!task.IsCompleted))
                                return await task.ConfigureAwait(false);
                        }
                        return val.Item2;
                    }
                }
                Interlocked.Increment(ref MissCount);
                ArgumentNullException.ThrowIfNull(func);
                if (waitUntilReady)
                {
                    var value = await func(key).ConfigureAwait(false);
                    val = ValueTuple.Create(GetExpirationDate(value), value, (Task<V>)null);
                    c[key] = val;
                    Q.Enqueue(ValueTuple.Create(val.Item1, key));
                    return val.Item2;
                }
                else
                {
                    ValueTask<V> work;
                    try
                    {
                        work = AsValueTask(func(key));
                    }
                    catch
                    {
                        c.TryRemove(key, out var _);
                        throw;
                    }
                    return StartBuild(key, work);
                }
            }
            finally
            {
                Unlock(key);
            }
        }

        #endregion//ValueTask

        #endregion//Without existing



        #endregion//Update



        long HitCount;
        long SemiHitCount;
        long MissCount;

        readonly ConcurrentDictionary<K, int> Locks;
        readonly ConcurrentDictionary<K, ValueTuple<DateTime, V, Task<V>>> C;
        readonly ConcurrentQueue<ValueTuple<DateTime, K>> Q = new ();

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
        /// Includes expired items that haven't been pruned yet and items being created in the background (expiration time is <see cref="DateTime.MaxValue"/> and the value is default)</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerator<(DateTime, K, V)> GetEnumerator() => C.Select(x => (x.Value.Item1, x.Key, x.Value.Item2)).GetEnumerator();

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    }



}
