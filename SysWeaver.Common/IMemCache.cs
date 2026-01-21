using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SysWeaver
{
    public interface IMemCache<K, V> : IEnumerable<ValueTuple<DateTime, K, V>>
    {
        /// <summary>
        /// Clear cached values
        /// </summary>
        void Clear();

        /// <summary>
        /// Get the count of cached items (somewhat slow)
        /// </summary>
        /// <returns>Number of cached items</returns>
        int GetCount();

        /// <summary>
        /// Get an item from the cache, if it doesn't exist in the cache, the supplied delegate is executed to create the item.
        /// Only one item can be created at the same time (locked using the key), so no risk for "double" effort. 
        /// </summary>
        /// <param name="key">Tke key</param>
        /// <param name="func">The delegate used to create a non-existing item</param>
        /// <returns>The value of the item</returns>
        V GetOrUpdate(K key, Func<K, V> func);

        /// <summary>
        /// Get an item from the cache, if it doesn't exist in the cache, the supplied delegate is executed to create the item.
        /// Only one item can be created at the same time (locked using the key), so no risk for "double" effort. 
        /// </summary>
        /// <param name="key">Tke key</param>
        /// <param name="func">The delegate used to create a non-existing item</param>
        /// <param name="arg">A custom argument that is passed to the delegate if invoked</param>
        /// <returns>The value of the item</returns>
        V GetOrUpdate<A>(K key, Func<K, A, V> func, A arg);

        /// <summary>
        /// Get an item from the cache, if it doesn't exist in the cache, the supplied delegate is executed to create the item.
        /// Only one item can be created at the same time (locked using the key), so no risk for "double" effort. 
        /// </summary>
        /// <param name="key">Tke key</param>
        /// <param name="func">The delegate used to create a non-existing item</param>
        /// <param name="arg0">A custom argument that is passed to the delegate if invoked</param>
        /// <param name="arg1">A custom argument that is passed to the delegate if invoked</param>
        /// <returns>The value of the item</returns>
        V GetOrUpdate<A0, A1>(K key, Func<K, A0, A1, V> func, A0 arg0, A1 arg1);


        /// <summary>
        /// Get an item from the cache, if it doesn't exist in the cache, the supplied delegate is executed to create the item.
        /// Only one item can be created at the same time (locked using the key), so no risk for "double" effort. 
        /// </summary>
        /// <param name="key">Tke key</param>
        /// <param name="func">The delegate used to create a non-existing item</param>
        /// <param name="waitUntilReady">If the item have to be updated, wait for the update before returning, else the default value will be returned and the update will be started concurrently</param>
        /// <returns>The value of the item or default if wait until ready is false and the update haven't completed yet</returns>
        Task<V> GetOrUpdateAsync(K key, Func<K, Task<V>> func, bool waitUntilReady = true);

        /// <summary>
        /// Get an item from the cache, if it doesn't exist in the cache, the supplied delegate is executed to create the item.
        /// Only one item can be created at the same time (locked using the key), so no risk for "double" effort. 
        /// </summary>
        /// <param name="key">Tke key</param>
        /// <param name="func">The delegate used to create a non-existing item</param>
        /// <param name="arg">A custom argument that is passed to the delegate if invoked</param>
        /// <param name="waitUntilReady">If the item have to be updated, wait for the update before returning, else the default value will be returned and the update will be started concurrently</param>
        /// <returns>The value of the item</returns>
        Task<V> GetOrUpdateAsync<A>(K key, Func<K, A, Task<V>> func, A arg, bool waitUntilReady = true);

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
        Task<V> GetOrUpdateAsync<A0, A1>(K key, Func<K, A0, A1, Task<V>> func, A0 arg0, A1 arg1, bool waitUntilReady = true);

        /// <summary>
        /// Get an item from the cache, if it doesn't exist in the cache, the supplied delegate is executed to create the item.
        /// Only one item can be created at the same time (locked using the key), so no risk for "double" effort. 
        /// </summary>
        /// <param name="key">Tke key</param>
        /// <param name="func">The delegate used to create a non-existing item</param>
        /// <param name="waitUntilReady">If the item have to be updated, wait for the update before returning, else the default value will be returned and the update will be started concurrently</param>
        /// <returns>The value of the item or default if wait until ready is false and the update haven't completed yet</returns>
        ValueTask<V> GetOrUpdateValueAsync(K key, Func<K, ValueTask<V>> func, bool waitUntilReady = true);

        /// <summary>
        /// Get an item from the cache, if it doesn't exist in the cache, the supplied delegate is executed to create the item.
        /// Only one item can be created at the same time (locked using the key), so no risk for "double" effort. 
        /// </summary>
        /// <param name="key">Tke key</param>
        /// <param name="func">The delegate used to create a non-existing item</param>
        /// <param name="arg">A custom argument that is passed to the delegate if invoked</param>
        /// <param name="waitUntilReady">If the item have to be updated, wait for the update before returning, else the default value will be returned and the update will be started concurrently</param>
        /// <returns>The value of the item</returns>
        ValueTask<V> GetOrUpdateValueAsync<A>(K key, Func<K, A, ValueTask<V>> func, A arg, bool waitUntilReady = true);

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
        ValueTask<V> GetOrUpdateValueAsync<A0, A1>(K key, Func<K, A0, A1, ValueTask<V>> func, A0 arg0, A1 arg1, bool waitUntilReady = true);

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
        long GetStats(out double hitRatio, out double semiHitRatio, out double missRatio, out long hitCount, out long semiHitCount, out long missCount, out long size);

        /// <summary>
        /// Get some stats about the cache performance
        /// </summary>
        /// <param name="hitCount">Number of cache hits (GetOrUpdate returns an existing item)</param>
        /// <param name="semiHitCount">Number of semi cache hits (GetOrUpdate returns an existing item, but had to take a lock to get it, so less optimal)</param>
        /// <param name="missCount">Number of cache misses (GetOrUpdate doesn't have an item, and a new one have to be created)</param>
        /// <param name="size">Number of items in the cache</param>
        void GetStats(out long hitCount, out long semiHitCount, out long missCount, out long size);

        /// <summary>
        /// Get some stats for the cache using Stats type
        /// </summary>
        /// <param name="system">A system name for the cache</param>
        /// <param name="prefix">An optional prefix to add to the stats name</param>
        /// <returns>Stats</returns>
        IEnumerable<Stats> GetStats(string system, string prefix = "");

        /// <summary>
        /// Call to manually prune (remove) old items, no real need to call this unless memory usage is the primary concern
        /// </summary>
        void Prune();

        /// <summary>
        /// Remove an entry from the cache
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        bool Remove(K key);

        /// <summary>
        /// Reset all stats counters
        /// </summary>
        void ResetStats();

        /// <summary>
        /// Set a new value
        /// </summary>
        /// <param name="key">Tke key</param>
        /// <param name="value">The new value</param>
        void Set(K key, V value);

        /// <summary>
        /// Get an item if it's cached
        /// </summary>
        /// <param name="key">Tke key</param>
        /// <param name="value">The cached value or default it it deosn't exist</param>
        /// <returns>True if a value exist</returns>
        bool TryGet(K key, out V value);
    }
}