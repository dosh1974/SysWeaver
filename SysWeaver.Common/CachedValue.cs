using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using SysWeaver.Data;

namespace SysWeaver
{
    /// <summary>
    /// Caches a single value with an expiration time, the value is (re-)created by a supplied function when it's missing or expired.
    /// </summary>
    /// <typeparam name="T">The type of the cached value</typeparam>
    /// <remarks>
    /// Thread safe. Reading a valid value is lock free, creating a value is serialized by an <see cref="AsyncLock"/> (so the value is only created once even with concurrent callers).
    /// If auto dispose is enabled the previous value is disposed when it's replaced, cleared or pruned (unless the same instance is cached again), exceptions thrown by dispose are recorded in <see cref="AutoDisposeErrors"/>.
    /// Values are not reference counted, another thread may have obtained a value just before it's replaced (and may still be using it),
    /// so the disposal of a replaced, cleared or pruned value is deferred by <see cref="DisposeDelay"/> (a grace period, by default the cache duration clamped to [10 seconds, 5 minutes]).
    /// A value that is used for longer than the grace period after it's replaced can still be disposed while in use, don't use auto dispose for such values.
    /// <see cref="Dispose"/> disposes the current value and all values waiting for a deferred disposal immediately.
    /// Exceptions thrown by the get function are propagated and nothing is cached.
    /// The async methods have overloads with a waitUntilReady argument: when false, a caller that finds the value missing or expired starts an update in the background
    /// (unless one is already running) and gets default until the value is ready, instead of waiting.
    /// </remarks>
    public sealed class CachedValue<T> : IDisposable
    {


        /// <summary>
        /// Create a value cache
        /// </summary>
        /// <param name="defaultCacheDuration">The duration to keep a cached version (if no explicit expiration time is supplied)</param>
        /// <param name="autoDispose">If true and <typeparamref name="T"/> implements <see cref="IDisposable"/>, a value is disposed when it's replaced or cleared (after a grace period, see <see cref="DisposeDelay"/>)</param>
        public CachedValue(TimeSpan defaultCacheDuration, bool autoDispose = true)
            : this(defaultCacheDuration, autoDispose, GetDefaultDisposeDelay(defaultCacheDuration))
        {
        }

        /// <summary>
        /// Create a value cache
        /// </summary>
        /// <param name="defaultCacheDuration">The duration to keep a cached version (if no explicit expiration time is supplied)</param>
        /// <param name="autoDispose">If true and <typeparamref name="T"/> implements <see cref="IDisposable"/>, a value is disposed when it's replaced or cleared (after <paramref name="disposeDelay"/>)</param>
        /// <param name="disposeDelay">The grace period before a replaced, cleared or pruned value is disposed (so that callers that obtained it just before can finish using it).
        /// Zero or negative disposes it immediately (once it's replaced).</param>
        public CachedValue(TimeSpan defaultCacheDuration, bool autoDispose, TimeSpan disposeDelay)
        {
            autoDispose &= typeof(IDisposable).IsAssignableFrom(typeof(T));
            DefaultCacheDuration = defaultCacheDuration;
            //  Task.Delay can't handle more than int.MaxValue - 1 milliseconds
            if (disposeDelay < TimeSpan.Zero)
                disposeDelay = TimeSpan.Zero;
            if (disposeDelay > MaxDisposeDelay)
                disposeDelay = MaxDisposeDelay;
            DisposeDelay = disposeDelay;
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
        /// Create a value cache with a default cache duration of 5 minutes
        /// </summary>
        /// <param name="autoDispose">If true and <typeparamref name="T"/> implements <see cref="IDisposable"/>, a value is disposed when it's replaced or cleared (after a grace period, see <see cref="DisposeDelay"/>)</param>
        public CachedValue(bool autoDispose = true) : this(TimeSpan.FromMinutes(5), autoDispose)
        {
        }

        /// <summary>
        /// The default grace period before a replaced value is disposed: the cache duration, clamped to [<see cref="MinDefaultDisposeDelay"/>, <see cref="MaxDefaultDisposeDelay"/>]
        /// </summary>
        /// <param name="defaultCacheDuration">The cache duration</param>
        /// <returns>The grace period</returns>
        static TimeSpan GetDefaultDisposeDelay(TimeSpan defaultCacheDuration)
        {
            if (defaultCacheDuration < MinDefaultDisposeDelay)
                return MinDefaultDisposeDelay;
            if (defaultCacheDuration > MaxDefaultDisposeDelay)
                return MaxDefaultDisposeDelay;
            return defaultCacheDuration;
        }

        /// <summary>
        /// The minimum default grace period before a replaced value is disposed
        /// </summary>
        public static readonly TimeSpan MinDefaultDisposeDelay = TimeSpan.FromSeconds(10);

        /// <summary>
        /// The maximum default grace period before a replaced value is disposed
        /// </summary>
        public static readonly TimeSpan MaxDefaultDisposeDelay = TimeSpan.FromMinutes(5);

        /// <summary>
        /// The maximum supported grace period (limit of <see cref="Task.Delay(TimeSpan)"/>)
        /// </summary>
        static readonly TimeSpan MaxDisposeDelay = TimeSpan.FromMilliseconds(int.MaxValue - 1);

        /// <summary>
        /// The grace period before a replaced, cleared or pruned value is disposed (if <see cref="WillDispose"/> is true), zero if it's disposed immediately.
        /// Callers that obtained the value just before it was replaced can use it during this time.
        /// </summary>
        public readonly TimeSpan DisposeDelay;

        /// <summary>
        /// True if values are disposed when they are replaced or cleared (auto dispose was requested and <typeparamref name="T"/> implements <see cref="IDisposable"/>)
        /// </summary>
        public readonly bool WillDispose;
        /// <summary>
        /// The duration to keep a value, used when the get function doesn't supply an explicit expiration time
        /// </summary>
        public readonly TimeSpan DefaultCacheDuration;
        /// <summary>
        /// Tracks exceptions thrown when disposing replaced values (null if <see cref="WillDispose"/> is false)
        /// </summary>
        public readonly ExceptionTracker AutoDisposeErrors;


        readonly Action<T> InternalDispose;

        /// <summary>
        /// Get the cached value (update invoked if invalid or non-existing)
        /// </summary>
        /// <param name="getFn">The function to call to get the original value (the value is cached for <see cref="DefaultCacheDuration"/>)</param>
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
            DisposeOld(old, val);
            return val;
        }

        /// <summary>
        /// Get the cached value (update invoked if invalid or non-existing)
        /// </summary>
        /// <param name="getFn">The function to call to get the original value and it's expiration time (UTC)</param>
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
            DisposeOld(old, val);
            return val;
        }

        /// <summary>
        /// Get the cached value (update invoked if invalid or non-existing)
        /// </summary>
        /// <param name="getFn">The function to call to get the original value and it's expiration time (UTC)</param>
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
            }
            DisposeOld(old, val);
            return val;
        }


        /// <summary>
        /// Get the cached value (update invoked if invalid or non-existing)
        /// </summary>
        /// <param name="getFn">The function to call to get the original value (the value is cached for <see cref="DefaultCacheDuration"/>)</param>
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
        /// <param name="getFn">The function to call to get the original value and it's expiration time (UTC)</param>
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
        /// <param name="getFn">The function to call to get the original value and it's expiration time (UTC)</param>
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
        /// <param name="getFn">The function to call to get the original value (the value is cached for <see cref="DefaultCacheDuration"/>)</param>
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
        /// <param name="getFn">The function to call to get the original value and it's expiration time (UTC)</param>
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
        /// <param name="getFn">The function to call to get the original value and it's expiration time (UTC)</param>
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
        /// Set a new value, cached for <see cref="DefaultCacheDuration"/>
        /// </summary>
        /// <param name="value">The new value</param>
        /// <remarks>If <see cref="WillDispose"/> is true the previous value is disposed after <see cref="DisposeDelay"/> (unless it's the same instance as <paramref name="value"/>), exceptions thrown by it are recorded in <see cref="AutoDisposeErrors"/></remarks>
        public void Set(T value)
        {
            Tuple<DateTime, T> old;
            using (var l = Lock.LockSync())
                old = Interlocked.Exchange(ref Data, Tuple.Create(DateTime.UtcNow + DefaultCacheDuration, value));
            DisposeOld(old, value);
        }

        /// <summary>
        /// Set a new value
        /// </summary>
        /// <param name="value">The new value</param>
        /// <param name="expirationTime">When this value will expire (UTC)</param>
        /// <remarks>If <see cref="WillDispose"/> is true the previous value is disposed after <see cref="DisposeDelay"/> (unless it's the same instance as <paramref name="value"/>), exceptions thrown by it are recorded in <see cref="AutoDisposeErrors"/></remarks>
        public void Set(T value, DateTime expirationTime)
        {
            Tuple<DateTime, T> old;
            using (var l = Lock.LockSync())
                old = Interlocked.Exchange(ref Data, Tuple.Create(expirationTime, value));
            DisposeOld(old, value);

        }

        /// <summary>
        /// Clear the cache (the next get will create a new value)
        /// </summary>
        /// <remarks>If <see cref="WillDispose"/> is true the previous value is disposed after <see cref="DisposeDelay"/>, exceptions thrown by it are recorded in <see cref="AutoDisposeErrors"/></remarks>
        public void Clear()
        {
            Tuple<DateTime, T> old;
            using (var l = Lock.LockSync())
                old = Interlocked.Exchange(ref Data, null);
            if (WillDispose && (old != null))
                DisposeLater(old.Item2);
        }


        /// <summary>
        /// Remove the value from the cache if it has expired (does nothing if the cache is empty)
        /// </summary>
        /// <remarks>If <see cref="WillDispose"/> is true the removed value is disposed after <see cref="DisposeDelay"/>, exceptions thrown by it are recorded in <see cref="AutoDisposeErrors"/></remarks>
        public void Prune()
        {
            Tuple<DateTime, T> old;
            using (var l = Lock.LockSync())
            {
                var d = Data;
                if ((d == null) || (DateTime.UtcNow < d.Item1))
                    return;
                old = Interlocked.Exchange(ref Data, null);
            }
            if (WillDispose && (old != null))
                DisposeLater(old.Item2);
        }

        /// <summary>
        /// Dispose a replaced value after <see cref="DisposeDelay"/> (if <see cref="WillDispose"/> is true), unless it's the same instance as the new value (that would dispose a value that is still cached)
        /// </summary>
        /// <param name="old">The replaced entry (can be null)</param>
        /// <param name="val">The new value</param>
        void DisposeOld(Tuple<DateTime, T> old, T val)
        {
            if (!WillDispose)
                return;
            //  The new value may be a previously replaced instance that is waiting to be disposed, it's cached again so it must not be disposed
            CancelDispose(val);
            if (old == null)
                return;
            var o = old.Item2;
            if (ReferenceEquals(o, val))
                return;
            DisposeLater(o);
        }

        /// <summary>
        /// Dispose a value that is no longer cached after <see cref="DisposeDelay"/> (immediately if it's zero).
        /// The value isn't reference counted, callers that obtained it just before it was replaced can use it during the grace period.
        /// </summary>
        /// <param name="value">The value to dispose (null is ignored)</param>
        void DisposeLater(T value)
        {
            if (value == null)
                return;
            var delay = DisposeDelay;
            if (delay <= TimeSpan.Zero)
            {
                InternalDispose(value);
                return;
            }
            var box = new StrongBox<T>(value);
            var p = PendingDisposals;
            lock (p)
                p.Add(box);
            _ = DelayedDispose(box, delay);
        }

        /// <summary>
        /// Wait for the grace period and dispose the value (unless it was cached again or already disposed by <see cref="Dispose"/>)
        /// </summary>
        /// <param name="box">The pending disposal</param>
        /// <param name="delay">The grace period</param>
        async Task DelayedDispose(StrongBox<T> box, TimeSpan delay)
        {
            await Task.Delay(delay).ConfigureAwait(false);
            var p = PendingDisposals;
            lock (p)
            {
                if (!p.Remove(box))
                    return;
            }
            var v = box.Value;
            //  Cached again (a concurrent Set of the same instance)
            var d = Data;
            if ((d != null) && ReferenceEquals(d.Item2, v))
                return;
            InternalDispose(v);
        }

        /// <summary>
        /// Cancel any pending disposal of a value (it's cached again)
        /// </summary>
        /// <param name="value">The value</param>
        void CancelDispose(T value)
        {
            if (value == null)
                return;
            var p = PendingDisposals;
            lock (p)
            {
                for (int i = p.Count - 1; i >= 0; --i)
                    if (ReferenceEquals(p[i].Value, value))
                        p.RemoveAt(i);
            }
        }

        /// <summary>
        /// Replaced values waiting for their grace period to end before they are disposed
        /// </summary>
        readonly List<StrongBox<T>> PendingDisposals = new List<StrongBox<T>>();

        /// <summary>
        /// If <see cref="WillDispose"/> is true the cache is cleared and the current value is disposed immediately (together with any replaced values waiting for their grace period to end),
        /// else nothing happens (the value remains cached)
        /// </summary>
        public void Dispose()
        {
            if (!WillDispose)
                return;
            Tuple<DateTime, T> old;
            using (var l = Lock.LockSync())
                old = Interlocked.Exchange(ref Data, null);
            StrongBox<T>[] pending;
            var p = PendingDisposals;
            lock (p)
            {
                pending = p.ToArray();
                p.Clear();
            }
            Object current = old == null ? null : (Object)old.Item2;
            foreach (var x in pending)
                if (!ReferenceEquals(x.Value, current))
                    InternalDispose(x.Value);
            if (old != null)
                InternalDispose(old.Item2);
        }

        /// <summary>
        /// Get some stats for the cache using Stats type (ratios are in percent)
        /// </summary>
        /// <param name="system">A system name for the cache</param>
        /// <param name="prefix">An optional prefix to add to the stats name</param>
        /// <returns>The total request count, followed by the hit, semi hit and miss ratios</returns>
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
        /// Reset all stats counters (not atomic with respect to concurrent requests)
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
            DisposeOld(old, val);
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
            DisposeOld(old, val);
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
            DisposeOld(old, val);
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
            DisposeOld(old, val);
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
            DisposeOld(old, val);
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
            DisposeOld(old, val);
            return val;
        }


        #region Optional waiting

        /// <summary>
        /// Get the cached value (update invoked if invalid or non-existing).
        /// </summary>
        /// <param name="getFn">The function to call to get the original value (the value is cached for <see cref="DefaultCacheDuration"/>)</param>
        /// <param name="waitUntilReady">If true, wait for the update (same as <see cref="GetOrUpdate(Func{Task{T}})"/>), else the update is started in the background (if not already running) and default is returned until it's ready</param>
        /// <returns>The cached value, or default if <paramref name="waitUntilReady"/> is false and the value isn't ready</returns>
        /// <remarks>Only one update runs at a time. Exceptions thrown by a background update are ignored (the next call starts a new update).</remarks>
        public ValueTask<T> GetOrUpdate(Func<Task<T>> getFn, bool waitUntilReady)
            => waitUntilReady ? GetOrUpdate(getFn) : GetOrStartUpdate(() => InternalGetOrUpdate(getFn));

        /// <summary>
        /// Get the cached value (update invoked if invalid or non-existing).
        /// </summary>
        /// <param name="getFn">The function to call to get the original value and the time when it expires (UTC)</param>
        /// <param name="waitUntilReady">If true, wait for the update (same as <see cref="GetOrUpdate(Func{Task{Tuple{DateTime, T}}})"/>), else the update is started in the background (if not already running) and default is returned until it's ready</param>
        /// <returns>The cached value, or default if <paramref name="waitUntilReady"/> is false and the value isn't ready</returns>
        /// <remarks>Only one update runs at a time. Exceptions thrown by a background update are ignored (the next call starts a new update).</remarks>
        public ValueTask<T> GetOrUpdate(Func<Task<Tuple<DateTime, T>>> getFn, bool waitUntilReady)
            => waitUntilReady ? GetOrUpdate(getFn) : GetOrStartUpdate(() => InternalGetOrUpdate(getFn));

        /// <summary>
        /// Get the cached value (update invoked if invalid or non-existing).
        /// </summary>
        /// <param name="getFn">The function to call to get the original value and the time when it expires (UTC)</param>
        /// <param name="waitUntilReady">If true, wait for the update (same as <see cref="GetOrUpdate(Func{Task{ValueTuple{DateTime, T}}})"/>), else the update is started in the background (if not already running) and default is returned until it's ready</param>
        /// <returns>The cached value, or default if <paramref name="waitUntilReady"/> is false and the value isn't ready</returns>
        /// <remarks>Only one update runs at a time. Exceptions thrown by a background update are ignored (the next call starts a new update).</remarks>
        public ValueTask<T> GetOrUpdate(Func<Task<ValueTuple<DateTime, T>>> getFn, bool waitUntilReady)
            => waitUntilReady ? GetOrUpdate(getFn) : GetOrStartUpdate(() => InternalGetOrUpdate(getFn));

        /// <summary>
        /// Get the cached value (update invoked if invalid or non-existing).
        /// </summary>
        /// <param name="getFn">The function to call to get the original value (the value is cached for <see cref="DefaultCacheDuration"/>)</param>
        /// <param name="waitUntilReady">If true, wait for the update (same as <see cref="GetOrUpdateValue(Func{ValueTask{T}})"/>), else the update is started in the background (if not already running) and default is returned until it's ready</param>
        /// <returns>The cached value, or default if <paramref name="waitUntilReady"/> is false and the value isn't ready</returns>
        /// <remarks>Only one update runs at a time. Exceptions thrown by a background update are ignored (the next call starts a new update).</remarks>
        public ValueTask<T> GetOrUpdateValue(Func<ValueTask<T>> getFn, bool waitUntilReady)
            => waitUntilReady ? GetOrUpdateValue(getFn) : GetOrStartUpdate(() => InternalGetOrUpdateValue(getFn));

        /// <summary>
        /// Get the cached value (update invoked if invalid or non-existing).
        /// </summary>
        /// <param name="getFn">The function to call to get the original value and the time when it expires (UTC)</param>
        /// <param name="waitUntilReady">If true, wait for the update (same as <see cref="GetOrUpdateValue(Func{ValueTask{Tuple{DateTime, T}}})"/>), else the update is started in the background (if not already running) and default is returned until it's ready</param>
        /// <returns>The cached value, or default if <paramref name="waitUntilReady"/> is false and the value isn't ready</returns>
        /// <remarks>Only one update runs at a time. Exceptions thrown by a background update are ignored (the next call starts a new update).</remarks>
        public ValueTask<T> GetOrUpdateValue(Func<ValueTask<Tuple<DateTime, T>>> getFn, bool waitUntilReady)
            => waitUntilReady ? GetOrUpdateValue(getFn) : GetOrStartUpdate(() => InternalGetOrUpdateValue(getFn));

        /// <summary>
        /// Get the cached value (update invoked if invalid or non-existing).
        /// </summary>
        /// <param name="getFn">The function to call to get the original value and the time when it expires (UTC)</param>
        /// <param name="waitUntilReady">If true, wait for the update (same as <see cref="GetOrUpdateValue(Func{ValueTask{ValueTuple{DateTime, T}}})"/>), else the update is started in the background (if not already running) and default is returned until it's ready</param>
        /// <returns>The cached value, or default if <paramref name="waitUntilReady"/> is false and the value isn't ready</returns>
        /// <remarks>Only one update runs at a time. Exceptions thrown by a background update are ignored (the next call starts a new update).</remarks>
        public ValueTask<T> GetOrUpdateValue(Func<ValueTask<ValueTuple<DateTime, T>>> getFn, bool waitUntilReady)
            => waitUntilReady ? GetOrUpdateValue(getFn) : GetOrStartUpdate(() => InternalGetOrUpdateValue(getFn));

        /// <summary>
        /// Return the value if it's valid, else start an update in the background (unless one is already running) and return default (or the value if the update completed synchronously).
        /// </summary>
        /// <param name="update">Runs the update (takes the lock, so it never runs concurrently with any other update)</param>
        ValueTask<T> GetOrStartUpdate(Func<ValueTask<T>> update)
        {
            var d = Data;
            if ((d != null) && (DateTime.UtcNow < d.Item1))
            {
                Interlocked.Increment(ref HitCount);
                return ValueTask.FromResult(d.Item2);
            }
            var p = PendingUpdate;
            if ((p == null) || p.IsCompleted)
            {
                var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                if (Interlocked.CompareExchange(ref PendingUpdate, tcs.Task, p) == p)
                    _ = RunUpdate(update, tcs);
            }
            //  The update may have completed synchronously
            d = Data;
            if ((d != null) && (DateTime.UtcNow < d.Item1))
                return ValueTask.FromResult(d.Item2);
            return default;
        }

        static async Task RunUpdate(Func<ValueTask<T>> update, TaskCompletionSource tcs)
        {
            try
            {
                await update().ConfigureAwait(false);
            }
            catch
            {
                //  Ignored, the next call starts a new update
            }
            finally
            {
                tcs.TrySetResult();
            }
        }

        /// <summary>
        /// The running background update (started by a caller that doesn't wait), null or completed if none is running
        /// </summary>
        Task PendingUpdate;

        #endregion//Optional waiting


        volatile Tuple<DateTime, T> Data;
        readonly AsyncLock Lock = new AsyncLock();

        long HitCount;
        long SemiHitCount;
        long MissCount;


        static readonly Action<T> NoDispose = val => { };


    }



}
