using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using SysWeaver.Data;

namespace SysWeaver
{
    public sealed class CachedValue<T> : IDisposable
    {


        /// <summary>
        /// Create a value cahce,
        /// </summary>
        /// <param name="defaultCacheDuration">The duration to keep a cached version (if no explicit expiration time is supplied)</param>
        /// <param name="autoDispose">If true and the value is disposable, the value will be dispose</param>
        public CachedValue(TimeSpan defaultCacheDuration, bool autoDispose = true)
        {
            autoDispose &= typeof(IDisposable).IsAssignableFrom(typeof(T));
            DefaultCacheDuration = defaultCacheDuration;
            if (autoDispose)
            {
                var e = new ExceptionTracker();
                AutoDisposeErrors = e;
                WillDispose = true;
                InternalDispose = val =>
                    {
                        try
                        {
                            (val as IDisposable)?.Dispose();
                        }
                        catch (Exception ex)
                        {
                            e.OnException(ex);
                        }
                    };
            }else
            {
                InternalDispose = NoDispose;
            }
        }


        /// <summary>
        /// Create a value cahce,
        /// </summary>
        /// <param name="autoDispose">If true and the value is disposable, the value will be dispose</param>
        public CachedValue(bool autoDispose = true) : this(TimeSpan.FromMinutes(5), autoDispose)
        {
        }

        public readonly bool WillDispose;
        public readonly TimeSpan DefaultCacheDuration;
        public readonly ExceptionTracker AutoDisposeErrors;


        readonly Action<T> InternalDispose;

        /// <summary>
        /// Get the cached value (update invoked if invalid or non-existing)
        /// </summary>
        /// <param name="getFn">The function to call to get the original value</param>
        /// <returns>The cached value</returns>
        public T GetOrUpdate(Func<T> getFn)
        {
            var d = Data;
            if (d != null)
                if (DateTime.UtcNow < d.Item1)
                {
                    Interlocked.Increment(ref HitCount);
                    return d.Item2;
                }
            Tuple<DateTime, T> old;
            T val;
            using (var l = Lock.LockSync())
            {
                d = Data;
                if (d != null)
                    if (DateTime.UtcNow < d.Item1)
                    {
                        Interlocked.Increment(ref SemiHitCount);
                        return d.Item2;
                    }
                Interlocked.Increment(ref MissCount);
                val = getFn();
                old = Interlocked.Exchange(ref Data, Tuple.Create(DateTime.UtcNow + DefaultCacheDuration, val));
            }
            if (WillDispose && (old != null))
                InternalDispose(old.Item2);
            return val;
        }

        /// <summary>
        /// Get the cached value (update invoked if invalid or non-existing)
        /// </summary>
        /// <param name="getFn">The function to call to get the original value and the expiration time</param>
        /// <returns>The cached value</returns>
        public T GetOrUpdate(Func<Tuple<DateTime, T>> getFn)
        {
            var d = Data;
            if (d != null)
                if (DateTime.UtcNow < d.Item1)
                {
                    Interlocked.Increment(ref HitCount);
                    return d.Item2;
                }
            Tuple<DateTime, T> old;
            T val;
            using (var l = Lock.LockSync())
            {
                d = Data;
                if (d != null)
                    if (DateTime.UtcNow < d.Item1)
                    {
                        Interlocked.Increment(ref SemiHitCount);
                        return d.Item2;
                    }
                Interlocked.Increment(ref MissCount);
                var valD = getFn();
                val = valD.Item2;
                old = Interlocked.Exchange(ref Data, valD);
            }
            if (WillDispose && (old != null))
                InternalDispose(old.Item2);
            return val;
        }

        /// <summary>
        /// Get the cached value (update invoked if invalid or non-existing)
        /// </summary>
        /// <param name="getFn">The function to call to get the original value and the expiration time</param>
        /// <returns>The cached value</returns>
        public T GetOrUpdate(Func<ValueTuple<DateTime, T>> getFn)
        {
            var d = Data;
            if (d != null)
                if (DateTime.UtcNow < d.Item1)
                {
                    Interlocked.Increment(ref HitCount);
                    return d.Item2;
                }
            Tuple<DateTime, T> old;
            T val;
            using (var l = Lock.LockSync())
            {
                d = Data;
                if (d != null)
                    if (DateTime.UtcNow < d.Item1)
                    {
                        Interlocked.Increment(ref SemiHitCount);
                        return d.Item2;
                    }
                Interlocked.Increment(ref MissCount);
                var valD = getFn();
                val = valD.Item2;
                old = Interlocked.Exchange(ref Data, Tuple.Create(valD.Item1, val));
                if (WillDispose && (old != null))
                    InternalDispose(old.Item2);
            }
            return val;
        }


        /// <summary>
        /// Get the cached value (update invoked if invalid or non-existing)
        /// </summary>
        /// <param name="getFn">The function to call to get the original value</param>
        /// <returns>The cached value</returns>
        public ValueTask<T> GetOrUpdate(Func<Task<T>> getFn)
        {
            var d = Data;
            if (d != null)
                if (DateTime.UtcNow < d.Item1)
                {
                    Interlocked.Increment(ref HitCount);
                    return ValueTask.FromResult(d.Item2);
                }
            return InternalGetOrUpdate(getFn);
        }

        /// <summary>
        /// Get the cached value (update invoked if invalid or non-existing)
        /// </summary>
        /// <param name="getFn">The function to call to get the original value and the expiration time</param>
        /// <returns>The cached value</returns>
        public ValueTask<T> GetOrUpdate(Func<Task<Tuple<DateTime, T>>> getFn)
        {
            var d = Data;
            if (d != null)
                if (DateTime.UtcNow < d.Item1)
                {
                    Interlocked.Increment(ref HitCount);
                    return ValueTask.FromResult(d.Item2);
                }
            return InternalGetOrUpdate(getFn);
        }



        /// <summary>
        /// Get the cached value (update invoked if invalid or non-existing)
        /// </summary>
        /// <param name="getFn">The function to call to get the original value and the expiration time</param>
        /// <returns>The cached value</returns>
        public ValueTask<T> GetOrUpdate(Func<Task<ValueTuple<DateTime, T>>> getFn)
        {
            var d = Data;
            if (d != null)
                if (DateTime.UtcNow < d.Item1)
                {
                    Interlocked.Increment(ref HitCount);
                    return ValueTask.FromResult(d.Item2);
                }
            return InternalGetOrUpdate(getFn);
        }


        /// <summary>
        /// Get the cached value (update invoked if invalid or non-existing)
        /// </summary>
        /// <param name="getFn">The function to call to get the original value</param>
        /// <returns>The cached value</returns>
        public ValueTask<T> GetOrUpdateValue(Func<ValueTask<T>> getFn)
        {
            var d = Data;
            if (d != null)
                if (DateTime.UtcNow < d.Item1)
                {
                    Interlocked.Increment(ref HitCount);
                    return ValueTask.FromResult(d.Item2);
                }
            return InternalGetOrUpdateValue(getFn);
        }




        /// <summary>
        /// Get the cached value (update invoked if invalid or non-existing)
        /// </summary>
        /// <param name="getFn">The function to call to get the original value and the expiration time</param>
        /// <returns>The cached value</returns>
        public ValueTask<T> GetOrUpdateValue(Func<ValueTask<Tuple<DateTime, T>>> getFn)
        {
            var d = Data;
            if (d != null)
                if (DateTime.UtcNow < d.Item1)
                {
                    Interlocked.Increment(ref HitCount);
                    return ValueTask.FromResult(d.Item2);
                }
            return InternalGetOrUpdateValue(getFn);
        }



        /// <summary>
        /// Get the cached value (update invoked if invalid or non-existing)
        /// </summary>
        /// <param name="getFn">The function to call to get the original value and the expiration time</param>
        /// <returns>The cached value</returns>
        public ValueTask<T> GetOrUpdateValue(Func<ValueTask<ValueTuple<DateTime, T>>> getFn)
        {
            var d = Data;
            if (d != null)
                if (DateTime.UtcNow < d.Item1)
                {
                    Interlocked.Increment(ref HitCount);
                    return ValueTask.FromResult(d.Item2);
                }
            return InternalGetOrUpdateValue(getFn);
        }


        /// <summary>
        /// Set a new value
        /// </summary>
        /// <param name="value">The new value</param>
        public void Set(T value)
        {
            Tuple<DateTime, T> old;
            using (var l = Lock.LockSync())
                old = Interlocked.Exchange(ref Data, Tuple.Create(DateTime.UtcNow + DefaultCacheDuration, value));
            if (WillDispose && (old != null))
                (old.Item2 as IDisposable).Dispose();
        }

        /// <summary>
        /// Set a new value
        /// </summary>
        /// <param name="value">The new value</param>
        /// <param name="expirationTime">When this value will expire</param>
        public void Set(T value, DateTime expirationTime)
        {
            Tuple<DateTime, T> old;
            using (var l = Lock.LockSync())
                old = Interlocked.Exchange(ref Data, Tuple.Create(expirationTime, value));
            if (WillDispose && (old != null))
                (old.Item2 as IDisposable).Dispose();

        }

        /// <summary>
        /// Clear the cache
        /// </summary>
        public void Clear()
        {
            Tuple<DateTime, T> old;
            using (var l = Lock.LockSync())
                old = Interlocked.Exchange(ref Data, null);
            if (WillDispose && (old != null))
                (old.Item2 as IDisposable).Dispose();
        }


        /// <summary>
        /// Remove the item from the cache if it's old
        /// </summary>
        public void Prune()
        {
            Tuple<DateTime, T> old;
            using (var l = Lock.LockSync())
            {
                var d = Data;
                if (DateTime.UtcNow < d.Item1)
                    return;
                old = Interlocked.Exchange(ref Data, null);
            }
            if (WillDispose && (old != null))
                (old.Item2 as IDisposable).Dispose();
        }

        public void Dispose()
        {
            if (WillDispose)
                Clear();
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
            var tot = h + s + m;
            var totOrg = tot;
            if (tot <= 0)
                tot = 1;
            yield return new Stats(system, prefix + "Total count", totOrg, "Number of times an item have been requested");
            yield return new Stats(system, prefix + "Hit ratio", (double)(((Decimal)h) * 100M / (Decimal)tot), "The ratio of cache hits (returns an existing item)", TableDataNumberAttribute.Percentage);
            yield return new Stats(system, prefix + "Semi hit ratio", (double)(((Decimal)s) * 100M / (Decimal)tot), "The ratio of semi cache hits (returns an existing item, but had to take a lock to get it, so less optimal)", TableDataNumberAttribute.Percentage);
            yield return new Stats(system, prefix + "Miss ratio", (double)(((Decimal)m) * 100M / (Decimal)tot), "The ratio of cache misses (doesn't have an item, and a new one have to be created)", TableDataNumberAttribute.Percentage);
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
        /// <returns>The total number of GetOrUpdate requests</returns>
        public long GetStats(
            out double hitRatio, out double semiHitRatio, out double missRatio,
            out long hitCount, out long semiHitCount, out long missCount)
        {
            hitCount = Interlocked.Read(ref HitCount);
            semiHitCount = Interlocked.Read(ref SemiHitCount);
            missCount = Interlocked.Read(ref MissCount);
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
        public void GetStats(
            out long hitCount, out long semiHitCount, out long missCount)
        {
            hitCount = Interlocked.Read(ref HitCount);
            semiHitCount = Interlocked.Read(ref SemiHitCount);
            missCount = Interlocked.Read(ref MissCount);
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


        async ValueTask<T> InternalGetOrUpdate(Func<Task<T>> getFn)
        {
            Tuple<DateTime, T> old;
            T val;
            using (var l = await Lock.Lock().ConfigureAwait(false))
            {
                var d = Data;
                if (d != null)
                    if (DateTime.UtcNow < d.Item1)
                    {
                        Interlocked.Increment(ref SemiHitCount);
                        return d.Item2;
                    }
                Interlocked.Increment(ref MissCount);
                val = await getFn().ConfigureAwait(false);
                old = Interlocked.Exchange(ref Data, Tuple.Create(DateTime.UtcNow + DefaultCacheDuration, val));
            }
            if (WillDispose && (old != null))
                InternalDispose(old.Item2);
            return val;
        }

        async ValueTask<T> InternalGetOrUpdate(Func<Task<Tuple<DateTime, T>>> getFn)
        {
            var d = Data;
            if (d != null)
                if (DateTime.UtcNow < d.Item1)
                {
                    Interlocked.Increment(ref HitCount);
                    return d.Item2;
                }
            Tuple<DateTime, T> old;
            T val;
            using (var l = await Lock.Lock().ConfigureAwait(false))
            {
                d = Data;
                if (d != null)
                    if (DateTime.UtcNow < d.Item1)
                    {
                        Interlocked.Increment(ref SemiHitCount);
                        return d.Item2;
                    }
                Interlocked.Increment(ref MissCount);
                var valD = await getFn().ConfigureAwait(false);
                val = valD.Item2;
                old = Interlocked.Exchange(ref Data, valD);
            }
            if (WillDispose && (old != null))
                InternalDispose(old.Item2);
            return val;
        }

        async ValueTask<T> InternalGetOrUpdate(Func<Task<ValueTuple<DateTime, T>>> getFn)
        {
            var d = Data;
            if (d != null)
                if (DateTime.UtcNow < d.Item1)
                {
                    Interlocked.Increment(ref HitCount);
                    return d.Item2;
                }
            Tuple<DateTime, T> old;
            T val;
            using (var l = await Lock.Lock().ConfigureAwait(false))
            {
                d = Data;
                if (d != null)
                    if (DateTime.UtcNow < d.Item1)
                    {
                        Interlocked.Increment(ref SemiHitCount);
                        return d.Item2;
                    }
                Interlocked.Increment(ref MissCount);
                var valD = await getFn().ConfigureAwait(false);
                val = valD.Item2;
                old = Interlocked.Exchange(ref Data, Tuple.Create(valD.Item1, val));
            }
            if (WillDispose && (old != null))
                InternalDispose(old.Item2);
            return val;
        }

        async ValueTask<T> InternalGetOrUpdateValue(Func<ValueTask<T>> getFn)
        {
            var d = Data;
            if (d != null)
                if (DateTime.UtcNow < d.Item1)
                {
                    Interlocked.Increment(ref HitCount);
                    return d.Item2;
                }
            Tuple<DateTime, T> old;
            T val;
            using (var l = await Lock.Lock().ConfigureAwait(false))
            {
                d = Data;
                if (d != null)
                    if (DateTime.UtcNow < d.Item1)
                    {
                        Interlocked.Increment(ref SemiHitCount);
                        return d.Item2;
                    }
                Interlocked.Increment(ref MissCount);
                val = await getFn().ConfigureAwait(false);
                old = Interlocked.Exchange(ref Data, Tuple.Create(DateTime.UtcNow + DefaultCacheDuration, val));
            }
            if (WillDispose && (old != null))
                InternalDispose(old.Item2);
            return val;
        }

        async ValueTask<T> InternalGetOrUpdateValue(Func<ValueTask<Tuple<DateTime, T>>> getFn)
        {
            var d = Data;
            if (d != null)
                if (DateTime.UtcNow < d.Item1)
                {
                    Interlocked.Increment(ref HitCount);
                    return d.Item2;
                }
            Tuple<DateTime, T> old;
            T val;
            using (var l = await Lock.Lock().ConfigureAwait(false))
            {
                d = Data;
                if (d != null)
                    if (DateTime.UtcNow < d.Item1)
                    {
                        Interlocked.Increment(ref SemiHitCount);
                        return d.Item2;
                    }
                Interlocked.Increment(ref MissCount);
                var valD = await getFn().ConfigureAwait(false);
                val = valD.Item2;
                old = Interlocked.Exchange(ref Data, valD);
            }
            if (WillDispose && (old != null))
                InternalDispose(old.Item2);
            return val;
        }

        async ValueTask<T> InternalGetOrUpdateValue(Func<ValueTask<ValueTuple<DateTime, T>>> getFn)
        {
            var d = Data;
            if (d != null)
                if (DateTime.UtcNow < d.Item1)
                {
                    Interlocked.Increment(ref HitCount);
                    return d.Item2;
                }
            Tuple<DateTime, T> old;
            T val;
            using (var l = await Lock.Lock().ConfigureAwait(false))
            {
                d = Data;
                if (d != null)
                    if (DateTime.UtcNow < d.Item1)
                    {
                        Interlocked.Increment(ref SemiHitCount);
                        return d.Item2;
                    }
                Interlocked.Increment(ref MissCount);
                var valD = await getFn().ConfigureAwait(false);
                val = valD.Item2;
                old = Interlocked.Exchange(ref Data, Tuple.Create(valD.Item1, val));
            }
            if (WillDispose && (old != null))
                InternalDispose(old.Item2);
            return val;
        }


        volatile Tuple<DateTime, T> Data;
        readonly AsyncLock Lock = new AsyncLock();

        long HitCount;
        long SemiHitCount;
        long MissCount;


        static readonly Action<T> NoDispose = val => { };


    }



}
