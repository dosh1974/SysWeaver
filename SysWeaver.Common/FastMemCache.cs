using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver
{

    /// <summary>
    /// Implements a cache that removes it's items after the specified duration (after last request)
    /// </summary>
    /// <typeparam name="K">The type of the key</typeparam>
    /// <typeparam name="V">The type of the value</typeparam>
    public sealed class FastMemCache<K, V>
    {
        
        /// <summary>
        /// Creates a cache that removes it's items after the specified duration (after last request)
        /// </summary>
        /// <param name="timeout">The duration to keep items in the cache (after last request)</param>
        /// <param name="comparer">An optional comparer</param>
        public FastMemCache(TimeSpan timeout, IEqualityComparer<K> comparer = null)
        {
            TimeOut = timeout;
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
        /// Get an item from the cache, if it doesn't exist in the cache, the supplied delegate is executed to create the item.
        /// Only one item can be created at the same time (locked using the key), so no risk for "double" effort. 
        /// </summary>
        /// <param name="key">Tke key</param>
        /// <param name="func">The delegate used to create a non-existing item</param>
        /// <returns>The value of the item</returns>
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
                val = ValueTuple.Create(DateTime.UtcNow + TimeOut, func(key), (Task<V>)null);
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
        /// Set a new value
        /// </summary>
        /// <param name="key">Tke key</param>
        /// <param name="value">The new value</param>
        public void Set(K key, V value)
        {
            var c = C;
            Lock(key);
            try
            {
                var val = ValueTuple.Create(DateTime.UtcNow + TimeOut, value, (Task<V>)null);
                c[key] = val;
                Q.Enqueue(ValueTuple.Create(val.Item1, key));
            }
            finally
            {
                Unlock(key);
            }
        }

        /// <summary>
        /// Get an item if it's cached
        /// </summary>
        /// <param name="key">Tke key</param>
        /// <param name="value">The cached value or default it it deosn't exist</param>
        /// <returns>True if a value exist</returns>
        public bool TryGet(K key, out V value)
        {
            if (!C.TryGetValue(key, out var v))
            {
                value = default;
                return false;
            }
            if (DateTime.UtcNow < v.Item1)
            {
                value = v.Item2;
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>
        /// Get an item from the cache, if it doesn't exist in the cache, the supplied delegate is executed to create the item.
        /// Only one item can be created at the same time (locked using the key), so no risk for "double" effort. 
        /// </summary>
        /// <param name="key">Tke key</param>
        /// <param name="func">The delegate used to create a non-existing item</param>
        /// <param name="arg">A custom argument that is passed to the delegate if invoked</param>
        /// <returns>The value of the item</returns>
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
                val = ValueTuple.Create(DateTime.UtcNow + TimeOut, func(key, arg), (Task<V>)null);
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
        /// <param name="key">Tke key</param>
        /// <param name="func">The delegate used to create a non-existing item</param>
        /// <param name="arg0">A custom argument that is passed to the delegate if invoked</param>
        /// <param name="arg1">A custom argument that is passed to the delegate if invoked</param>
        /// <returns>The value of the item</returns>
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
                val = ValueTuple.Create(DateTime.UtcNow + TimeOut, func(key, arg0, arg1), (Task<V>)null);
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
        /// <param name="key">Tke key</param>
        /// <param name="func">The delegate used to create a non-existing item</param>
        /// <param name="waitUntilReady">If the item have to be updated, wait for the update before returning, else the default value will be returned and the update will be started concurrently</param>
        /// <returns>The value of the item or default if wait until ready is false and the update haven't completed yet</returns>
        public async Task<V> GetOrUpdateAsync(K key, Func<K, Task<V>> func, bool waitUntilReady = true)
        {
            var c = C;
            if (c.TryGetValue(key, out var val))
            {
                if (DateTime.UtcNow < val.Item1)
                {
                    Interlocked.Increment(ref HitCount);
                    if (waitUntilReady)
                    {
                        var task = val.Item3;
                        if (task != null)
                            return await task.ConfigureAwait(false);
                    }
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
                        if (waitUntilReady)
                        {
                            var task = val.Item3;
                            if (task != null)
                            {
                                Unlock(key);
                                return await task.ConfigureAwait(false);
                            }
                        }
                        return val.Item2;
                    }
                }
                Interlocked.Increment(ref MissCount);
                if (waitUntilReady)
                {
                    val = ValueTuple.Create(DateTime.UtcNow + TimeOut, await func(key).ConfigureAwait(false), (Task<V>)null);
                    c[key] = val;
                    Q.Enqueue(ValueTuple.Create(val.Item1, key));
                    return val.Item2;
                }else
                {
                    async Task<V> build()
                    {
                        V v = default;
                        try
                        {
                            v = await func(key).ConfigureAwait(false);
                        }
                        catch
                        {
                            c.TryRemove(key, out var _);
                            throw;
                        }
                        val = ValueTuple.Create(DateTime.UtcNow + TimeOut, v, (Task<V>)null);
                        try
                        {
                            c[key] = val;
                            Q.Enqueue(ValueTuple.Create(val.Item1, key));
                        }
                        finally
                        {
                        }
                        return v;
                    }
                    var task = build();
                    if (task.IsCompleted)
                        return task.Result;
                    val = ValueTuple.Create(DateTime.MaxValue, default(V), task);
                    c[key] = val;
                    TaskExt.StartNewAsyncChain(() => task);
                    return default(V);
                }
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
        /// <param name="key">Tke key</param>
        /// <param name="func">The delegate used to create a non-existing item</param>
        /// <param name="arg">A custom argument that is passed to the delegate if invoked</param>
        /// <param name="waitUntilReady">If the item have to be updated, wait for the update before returning, else the default value will be returned and the update will be started concurrently</param>
        /// <returns>The value of the item</returns>
        public async Task<V> GetOrUpdateAsync<A>(K key, Func<K, A, Task<V>> func, A arg, bool waitUntilReady = true)
        {
            var c = C;
            if (c.TryGetValue(key, out var val))
            {
                if (DateTime.UtcNow < val.Item1)
                {
                    Interlocked.Increment(ref HitCount);
                    if (waitUntilReady)
                    {
                        var task = val.Item3;
                        if (task != null)
                            return await task.ConfigureAwait(false);
                    }
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
                        if (waitUntilReady)
                        {
                            var task = val.Item3;
                            if (task != null)
                            {
                                Unlock(key);
                                return await task.ConfigureAwait(false);
                            }
                        }
                        return val.Item2;
                    }
                }
                Interlocked.Increment(ref MissCount);
                if (waitUntilReady)
                {
                    val = ValueTuple.Create(DateTime.UtcNow + TimeOut, await func(key, arg).ConfigureAwait(false), (Task<V>)null);
                    c[key] = val;
                    Q.Enqueue(ValueTuple.Create(val.Item1, key));
                    return val.Item2;
                }
                else
                {
                    async Task<V> build()
                    {
                        V v = default;
                        try
                        {
                            v = await func(key, arg).ConfigureAwait(false);
                        }
                        catch
                        {
                            c.TryRemove(key, out var _);
                            throw;
                        }
                        val = ValueTuple.Create(DateTime.UtcNow + TimeOut, v, (Task<V>)null);
                        try
                        {
                            c[key] = val;
                            Q.Enqueue(ValueTuple.Create(val.Item1, key));
                        }
                        finally
                        {
                        }
                        return v;
                    }
                    var task = build();
                    if (task.IsCompleted)
                        return task.Result;
                    val = ValueTuple.Create(DateTime.MaxValue, default(V), task);
                    c[key] = val;
                    TaskExt.StartNewAsyncChain(() => task);
                    return default(V);
                }
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
        /// <param name="key">Tke key</param>
        /// <param name="func">The delegate used to create a non-existing item</param>
        /// <param name="arg0">A custom argument that is passed to the delegate if invoked</param>
        /// <param name="arg1">A custom argument that is passed to the delegate if invoked</param>
        /// <param name="waitUntilReady">If the item have to be updated, wait for the update before returning, else the default value will be returned and the update will be started concurrently</param>
        /// <returns>The value of the item</returns>
        public async Task<V> GetOrUpdateAsync<A0, A1>(K key, Func<K, A0, A1, Task<V>> func, A0 arg0, A1 arg1, bool waitUntilReady = true)
        {
            var c = C;
            if (c.TryGetValue(key, out var val))
            {
                if (DateTime.UtcNow < val.Item1)
                {
                    Interlocked.Increment(ref HitCount);
                    if (waitUntilReady)
                    {
                        var task = val.Item3;
                        if (task != null)
                            return await task.ConfigureAwait(false);
                    }
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
                        if (waitUntilReady)
                        {
                            var task = val.Item3;
                            if (task != null)
                            {
                                Unlock(key);
                                return await task.ConfigureAwait(false);
                            }
                        }
                        return val.Item2;
                    }
                }
                Interlocked.Increment(ref MissCount);
                if (waitUntilReady)
                {
                    val = ValueTuple.Create(DateTime.UtcNow + TimeOut, await func(key, arg0, arg1).ConfigureAwait(false), (Task<V>)null);
                    c[key] = val;
                    Q.Enqueue(ValueTuple.Create(val.Item1, key));
                    return val.Item2;
                }
                else
                {
                    async Task<V> build()
                    {
                        V v = default;
                        try
                        {
                            v = await func(key, arg0, arg1).ConfigureAwait(false);
                        }
                        catch
                        {
                            c.TryRemove(key, out var _);
                            throw;
                        }
                        val = ValueTuple.Create(DateTime.UtcNow + TimeOut, v, (Task<V>)null);
                        try
                        {
                            c[key] = val;
                            Q.Enqueue(ValueTuple.Create(val.Item1, key));
                        }
                        finally
                        {
                        }
                        return v;
                    }
                    var task = build();
                    if (task.IsCompleted)
                        return task.Result;
                    val = ValueTuple.Create(DateTime.MaxValue, default(V), task);
                    c[key] = val;
                    TaskExt.StartNewAsyncChain(() => task);
                    return default(V);
                }
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
        /// <param name="key">Tke key</param>
        /// <param name="func">The delegate used to create a non-existing item</param>
        /// <param name="waitUntilReady">If the item have to be updated, wait for the update before returning, else the default value will be returned and the update will be started concurrently</param>
        /// <returns>The value of the item or default if wait until ready is false and the update haven't completed yet</returns>
        public async ValueTask<V> GetOrUpdateValueAsync(K key, Func<K, ValueTask<V>> func, bool waitUntilReady = true)
        {
            var c = C;
            if (c.TryGetValue(key, out var val))
            {
                if (DateTime.UtcNow < val.Item1)
                {
                    Interlocked.Increment(ref HitCount);
                    if (waitUntilReady)
                    {
                        var task = val.Item3;
                        if (task != null)
                            return await task.ConfigureAwait(false);
                    }
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
                        if (waitUntilReady)
                        {
                            var task = val.Item3;
                            if (task != null)
                            {
                                Unlock(key);
                                return await task.ConfigureAwait(false);
                            }
                        }
                        return val.Item2;
                    }
                }
                Interlocked.Increment(ref MissCount);
                if (waitUntilReady)
                {
                    val = ValueTuple.Create(DateTime.UtcNow + TimeOut, await func(key).ConfigureAwait(false), (Task<V>)null);
                    c[key] = val;
                    Q.Enqueue(ValueTuple.Create(val.Item1, key));
                    return val.Item2;
                }
                else
                {
                    async Task<V> build()
                    {
                        V v = default;
                        try
                        {
                            v = await func(key).ConfigureAwait(false);
                        }
                        catch
                        {
                            c.TryRemove(key, out var _);
                            throw;
                        }
                        val = ValueTuple.Create(DateTime.UtcNow + TimeOut, v, (Task<V>)null);
                        try
                        {
                            c[key] = val;
                            Q.Enqueue(ValueTuple.Create(val.Item1, key));
                        }
                        finally
                        {
                        }
                        return v;
                    }
                    var task = build();
                    if (task.IsCompleted)
                        return task.Result;
                    val = ValueTuple.Create(DateTime.MaxValue, default(V), task);
                    c[key] = val;
                    TaskExt.StartNewAsyncChain(() => task);
                    return default(V);
                }
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
        /// <param name="key">Tke key</param>
        /// <param name="func">The delegate used to create a non-existing item</param>
        /// <param name="arg">A custom argument that is passed to the delegate if invoked</param>
        /// <param name="waitUntilReady">If the item have to be updated, wait for the update before returning, else the default value will be returned and the update will be started concurrently</param>
        /// <returns>The value of the item</returns>
        public async ValueTask<V> GetOrUpdateValueAsync<A>(K key, Func<K, A, ValueTask<V>> func, A arg, bool waitUntilReady = true)
        {
            var c = C;
            if (c.TryGetValue(key, out var val))
            {
                if (DateTime.UtcNow < val.Item1)
                {
                    Interlocked.Increment(ref HitCount);
                    if (waitUntilReady)
                    {
                        var task = val.Item3;
                        if (task != null)
                            return await task.ConfigureAwait(false);
                    }
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
                        if (waitUntilReady)
                        {
                            var task = val.Item3;
                            if (task != null)
                            {
                                Unlock(key);
                                return await task.ConfigureAwait(false);
                            }
                        }
                        return val.Item2;
                    }
                }
                Interlocked.Increment(ref MissCount);
                if (waitUntilReady)
                {
                    val = ValueTuple.Create(DateTime.UtcNow + TimeOut, await func(key, arg).ConfigureAwait(false), (Task<V>)null);
                    c[key] = val;
                    Q.Enqueue(ValueTuple.Create(val.Item1, key));
                    return val.Item2;
                }
                else
                {
                    async Task<V> build()
                    {
                        V v = default;
                        try
                        {
                            v = await func(key, arg).ConfigureAwait(false);
                        }
                        catch
                        {
                            c.TryRemove(key, out var _);
                            throw;
                        }
                        val = ValueTuple.Create(DateTime.UtcNow + TimeOut, v, (Task<V>)null);
                        try
                        {
                            c[key] = val;
                            Q.Enqueue(ValueTuple.Create(val.Item1, key));
                        }
                        finally
                        {
                        }
                        return v;
                    }
                    var task = build();
                    if (task.IsCompleted)
                        return task.Result;
                    val = ValueTuple.Create(DateTime.MaxValue, default(V), task);
                    c[key] = val;
                    TaskExt.StartNewAsyncChain(() => task);
                    return default(V);
                }
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
        /// <param name="key">Tke key</param>
        /// <param name="func">The delegate used to create a non-existing item</param>
        /// <param name="arg0">A custom argument that is passed to the delegate if invoked</param>
        /// <param name="arg1">A custom argument that is passed to the delegate if invoked</param>
        /// <param name="waitUntilReady">If the item have to be updated, wait for the update before returning, else the default value will be returned and the update will be started concurrently</param>
        /// <returns>The value of the item</returns>
        public async ValueTask<V> GetOrUpdateValueAsync<A0, A1>(K key, Func<K, A0, A1, ValueTask<V>> func, A0 arg0, A1 arg1, bool waitUntilReady = true)
        {
            var c = C;
            if (c.TryGetValue(key, out var val))
            {
                if (DateTime.UtcNow < val.Item1)
                {
                    Interlocked.Increment(ref HitCount);
                    if (waitUntilReady)
                    {
                        var task = val.Item3;
                        if (task != null)
                            return await task.ConfigureAwait(false);
                    }
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
                        if (waitUntilReady)
                        {
                            var task = val.Item3;
                            if (task != null)
                            {
                                Unlock(key);
                                return await task.ConfigureAwait(false);
                            }
                        }
                        return val.Item2;
                    }
                }
                Interlocked.Increment(ref MissCount);
                if (waitUntilReady)
                {
                    val = ValueTuple.Create(DateTime.UtcNow + TimeOut, await func(key, arg0, arg1).ConfigureAwait(false), (Task<V>)null);
                    c[key] = val;
                    Q.Enqueue(ValueTuple.Create(val.Item1, key));
                    return val.Item2;
                }
                else
                {
                    async Task<V> build()
                    {
                        V v = default;
                        try
                        {
                            v = await func(key, arg0, arg1).ConfigureAwait(false);
                        }
                        catch
                        {
                            c.TryRemove(key, out var _);
                            throw;
                        }
                        val = ValueTuple.Create(DateTime.UtcNow + TimeOut, v, (Task<V>)null);
                        try
                        {
                            c[key] = val;
                            Q.Enqueue(ValueTuple.Create(val.Item1, key));
                        }
                        finally
                        {
                        }
                        return v;
                    }
                    var task = build();
                    if (task.IsCompleted)
                        return task.Result;
                    val = ValueTuple.Create(DateTime.MaxValue, default(V), task);
                    c[key] = val;
                    TaskExt.StartNewAsyncChain(() => task);
                    return default(V);
                }
            }
            finally
            {
                Unlock(key);
            }
        }



        /// <summary>
        /// Call to manually prune (remove) old items, no real need to call this unless memory usage is the primary concern
        /// </summary>
        public void Prune()
        {
            var q = Q;
            if (!q.TryPeek(out var v))
                return;
            var exp = DateTime.UtcNow;
            if (exp < v.Item1)
                return;
            var c = C;
            var locks = Locks;
            lock (q)
            {
                for (; ; )
                {
                    if (!q.TryPeek(out v))
                        return;
                    if (exp < v.Item1)
                        return;
                    q.TryDequeue(out v);
                    var key = v.Item2;
                    SpinWait.SpinUntil(() => locks.TryAdd(key, 0));
                    if (c.TryGetValue(key, out var val))
                    {
                        if (val.Item1 == v.Item1)
                            c.TryRemove(key, out var _);
                    }
                    locks.TryRemove(key, out var _);
                }
            }
        }


        /// <summary>
        /// Remove an entry from the cache
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
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
        public void Clear()
        {
            var q = Q;
            var c = C;
            var locks = Locks;
            lock (q)
            {
                for (; ; )
                {
                    if (!q.TryDequeue(out var v))
                        return;
                    var key = v.Item2;
                    SpinWait.SpinUntil(() => locks.TryAdd(key, 0));
                    if (c.TryGetValue(key, out var val))
                    {
                        if (val.Item1 == v.Item1)
                            c.TryRemove(key, out var _);
                    }
                    locks.TryRemove(key, out var _);
                }
            }
        }

        /// <summary>
        /// Get some stats for the cache using Stats type
        /// </summary>
        /// <param name="system">A system name for the cache</param>
        /// <param name="prefix">An optional prefix to add to the stats name</param>
        /// <returns>Stats</returns>
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

        void Lock(K key)
        {
            Prune();
            SpinWait.SpinUntil(() => Locks.TryAdd(key, 0));
        }

        void Unlock(K key)
        {
            Locks.TryRemove(key, out var _);
        }

        long HitCount;
        long SemiHitCount;
        long MissCount;

        readonly ConcurrentDictionary<K, int> Locks;
        readonly ConcurrentDictionary<K, ValueTuple<DateTime, V, Task<V>>> C;
        readonly ConcurrentQueue<ValueTuple<DateTime, K>> Q = new ConcurrentQueue<(DateTime, K)>();

        readonly TimeSpan TimeOut;

        /// <summary>
        /// Get the count of cached items (somewhat slow)
        /// </summary>
        /// <returns>Number of cached items</returns>
        public int GetCount() => C.Count;
    }

}
