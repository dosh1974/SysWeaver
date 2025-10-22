using Dapper;
using SimpleStack.Orm;
using SimpleStack.Orm.Expressions.Statements.Typed;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver.Db
{

    /// <summary>
    /// Utiltiy function to create a database cache (in a key/value dictionary). 
    /// </summary>
    public static class DbTableCache
    {
        /// <summary>
        /// Create an in memory cache of the complete database table (in a key/value dictionary).
        /// </summary>
        /// <typeparam name="K">Key type</typeparam>
        /// <typeparam name="T">Database row type</typeparam>
        /// <param name="db">The database to read from</param>
        /// <param name="extractKey">The function that extract the dictionary key for the given data</param>
        /// <param name="refreshEveryMs">Delay between each refresh, if 0 or less, no automatic refreshing is done</param>
        /// <param name="comparer">An optional comparer to use</param>
        /// <param name="tableName">An optional table to read from</param>
        /// <param name="refineSelect">An optional action that can be used to filter the select query</param>
        /// <returns>A database cache</returns>
        public static DbTableCache<K, T> Create<K, T>(DbSimpleStack db, Func<T, K> extractKey, int refreshEveryMs = 30000, IEqualityComparer<K> comparer = null, String tableName = null, Action<TypedSelectStatement<T>, IDialectProvider> refineSelect = null) where T : class, new()
            => new DbTableCache<K, T>(db, extractKey, refreshEveryMs, comparer, tableName, refineSelect);
    }

    /// <summary>
    /// Utiltiy function to create a database cache (in a key/value dictionary). 
    /// </summary>
    /// <typeparam name="T">Database row type</typeparam>
    public static class DbTableCache<T> where T : class, new()
    {
        /// <summary>
        /// Create an in memory cache of the complete database table (in a key/value dictionary).
        /// </summary>
        /// <typeparam name="K">Key type</typeparam>
        /// <param name="db">The database to read from</param>
        /// <param name="extractKey">The function that extract the dictionary key for the given data</param>
        /// <param name="refreshEveryMs">Delay between each refresh, if 0 or less, no automatic refreshing is done</param>
        /// <param name="comparer">An optional comparer to use</param>
        /// <param name="tableName">An optional table to read from</param>
        /// <param name="refineSelect">An optional action that can be used to filter the select query</param>
        /// <returns>A database cache</returns>
        public static DbTableCache<K, T> Create<K>(DbSimpleStack db, Func<T, K> extractKey, int refreshEveryMs = 30000, IEqualityComparer<K> comparer = null, String tableName = null, Action<TypedSelectStatement<T>, IDialectProvider> refineSelect = null)
            => new DbTableCache<K, T>(db, extractKey, refreshEveryMs, comparer, tableName, refineSelect);
    }

    public interface IDbTableCache : IDisposable, IPerfMonitored
    {
        /// <summary>
        /// Explicitly sync, call once after creation to populate the cache
        /// </summary>
        /// <returns></returns>
        Task SyncNow();

        /// <summary>
        /// Explicitly sync using an explicit db connection, call once after creation to populate the cache
        /// </summary>
        /// <param name="c">The db connection to use</param>
        /// <returns></returns>
        Task SyncNow(OrmConnection c);
    }


    public sealed class DbCachedValues<K, T>  where T : class, new()
    {
        public override string ToString() => String.Concat(Values.Count, " @ ", SyncStart, " to ", SyncEnd);
        /// <summary>
        /// The values
        /// </summary>
        public readonly IReadOnlyDictionary<K, T> Values;
        /// <summary>
        /// Time stamp when the db read begun
        /// </summary>
        public readonly DateTime SyncStart;
        /// <summary>
        /// Time stamp when the db read ended
        /// </summary>
        public readonly DateTime SyncEnd;


        public DbCachedValues(IReadOnlyDictionary<K, T> values, DateTime syncStart, DateTime syncEnd)
        {
            Values = values;
            SyncStart = syncStart;
            SyncEnd = syncEnd;
        }
    }


    /// <summary>
    /// Type that caches an entire database table in memory (in a key/value dictionary).
    /// Optionally updates it in the background.
    /// </summary>
    /// <typeparam name="K">Key type</typeparam>
    /// <typeparam name="T">Database row type</typeparam>
    public class DbTableCache<K, T> : IDbTableCache where T : class, new()
    {
        public PerfMonitor PerfMon { get; init; }


        /// <summary>
        /// Create an in memory cache of the complete database table (in a key/value dictionary).
        /// </summary>
        /// <param name="db">The database to read from</param>
        /// <param name="extractKey">The function that extract the dictionary key for the given data</param>
        /// <param name="refreshEveryMs">Delay between each refresh, if 0 or less, no automatic refreshing is done</param>
        /// <param name="comparer">An optional comparer to use</param>
        /// <param name="tableName">An optional table to read from</param>
        /// <param name="refineSelect">An optional action that can be used to filter the select query</param>
        /// <param name="perfMon">Optional performance mointor instance to use, if null an new internal instance will be created</param>
        public DbTableCache(DbSimpleStack db, Func<T, K> extractKey, int refreshEveryMs = 30000, IEqualityComparer<K> comparer = null, String tableName = null, Action<TypedSelectStatement<T>, IDialectProvider> refineSelect = null, PerfMonitor perfMon = null)
        {
            Db = db;
            var t = "DbTableCache_" + typeof(T).Name + (tableName != null ? (" @ " + tableName.ToQuoted()) : "");
            PerfNamePrefix = perfMon == null ? "" : (t + '.');
            PerfMon = perfMon ?? new PerfMonitor(t);
            ExtractKey = extractKey;
            Comparer = comparer ?? EqualityComparer<K>.Default;
            TableName = tableName;
            RefineSelect = refineSelect;
            if (refreshEveryMs > 0)
                UpdateTask = new PeriodicTask(Update, refreshEveryMs, true, true, true);
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref UpdateTask, null)?.Dispose();
        }

        PeriodicTask UpdateTask;
        readonly String PerfNamePrefix;
        readonly Func<T, K> ExtractKey;
        readonly Action<TypedSelectStatement<T>, IDialectProvider> RefineSelect;
        readonly DbSimpleStack Db;
        readonly IEqualityComparer<K> Comparer;
        readonly String TableName;


        /// <summary>
        /// Get the current values (if no sync have been performed, this will block until a sync have been done)
        /// </summary>
        public IReadOnlyDictionary<K, T> Values
        {
            get
            {
                var c = InternalValues;
                if (c == null)
                {
                    SyncNow().RunAsync();
                    c = InternalValues;
                }
                return c.Values;
            }
        }

        /// <summary>
        /// Get the current state (if no sync have been performed, this will block until a sync have been done)
        /// </summary>
        public DbCachedValues<K, T> Current
        {
            get
            {
                var c = InternalValues;
                if (c == null)
                {
                    SyncNow().RunAsync();
                    c = InternalValues;
                }
                return c;
            }
        }

        volatile DbCachedValues<K, T> InternalValues;

       
        async ValueTask<bool> Update()
        {
            await SyncNow().ConfigureAwait(false);
            return true;
        }

        /// <summary>
        /// Explicitly sync, call once after creation to populate the cache
        /// </summary>
        /// <returns></returns>
        public async Task SyncNow()
        {
            using var _ = PerfMon.Track(PerfNamePrefix + nameof(SyncNow));
            var d = new Dictionary<K, T>(Comparer);
            var extractKey = ExtractKey;
            DateTime start;
            DateTime end;
            using (var c = await Db.GetAsync().ConfigureAwait(false))
            {
                var dp = c.DialectProvider;
                var s = new TypedSelectStatement<T>(dp);
                if (!String.IsNullOrEmpty(TableName))
                    s.From(TableName);
                RefineSelect?.Invoke(s, dp);
                var cmd = dp.ToSelectStatement(s.Statement, CommandFlags.Buffered);
                start = DateTime.UtcNow;                
                using var reader = await c.ExecuteReaderAsync(cmd).ConfigureAwait(false);
                while (await reader.ReadAsync().ConfigureAwait(false))
                {
                    var value = reader.CreateObject<T>();
                    var key = extractKey(value);
                    d[key] = value;
                }
                end = DateTime.UtcNow;
            }
            var fd = d.Freeze();
            var newVal = new DbCachedValues<K, T>(fd, start, end);
            Interlocked.Exchange(ref InternalValues, newVal);
            var os = OnSynced;
            var osa = OnSyncedAsync;
            if ((os != null) || (osa != null))
            {
                using var __ = PerfMon.Track(String.Concat(PerfNamePrefix, nameof(SyncNow), ".Events"));
                try
                {
                    os?.Invoke(newVal);
                }
                catch
                {
                }
                try
                {
                    await osa.RaiseEvents(newVal).ConfigureAwait(false);
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// Explicitly sync using an explicit db connection, call once after creation to populate the cache
        /// </summary>
        /// <param name="c">The db connection to use</param>
        /// <returns></returns>
        public async Task SyncNow(OrmConnection c)
        {
            using var _ = PerfMon.Track(PerfNamePrefix + nameof(SyncNow));
            var d = new Dictionary<K, T>(Comparer);
            var extractKey = ExtractKey;
            var dp = c.DialectProvider;
            var s = new TypedSelectStatement<T>(dp);
            if (!String.IsNullOrEmpty(TableName))
                s.From(TableName);
            RefineSelect?.Invoke(s, dp);
            var cmd = dp.ToSelectStatement(s.Statement, CommandFlags.Buffered);
            DateTime start = DateTime.UtcNow;
            using var reader = await c.ExecuteReaderAsync(cmd).ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                var value = reader.CreateObject<T>();
                var key = extractKey(value);
                d[key] = value;
            }
            DateTime end = DateTime.UtcNow;
            var fd = d.Freeze();
            var newVal = new DbCachedValues<K, T>(fd, start, end);
            Interlocked.Exchange(ref InternalValues, newVal);
            var os = OnSynced;
            var osa = OnSyncedAsync;
            if ((os != null) || (osa != null))
            {
                using var __ = PerfMon.Track(String.Concat(PerfNamePrefix, nameof(SyncNow), ".Events"));
                try
                {
                    os?.Invoke(newVal);
                }
                catch
                {
                }
                try
                {
                    await osa.RaiseEvents(newVal).ConfigureAwait(false);
                }
                catch
                {
                }
            }

        }

        public event Action<DbCachedValues<K, T>> OnSynced;
        public event Func<DbCachedValues<K, T>, Task> OnSyncedAsync;


    }

}