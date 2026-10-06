using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SysWeaver.Data;

namespace SysWeaver.Data
{
    /// <summary>
    /// A thread safe store of <see cref="DataReference"/> instances for one <see cref="DataScopes"/>.
    /// The HTTP server keeps one storage per scope (global, any user) and one per session.
    /// </summary>
    /// <remarks>
    /// Ids are generated from an incrementing counter (seeded from the current time) encoded with <see cref="CompactAsciiString.Secure"/>
    /// (a URL/HTML safe char set, not a cryptographic transform), prefixed with the scope char from <see cref="DataScopeTools.ScopePrefixes"/>.
    /// The ids are unique but sequential and therefore guessable; access control must be enforced by the caller choosing the storage.
    /// </remarks>
    public sealed class DataReferenceStorage : IDisposable
    {
        /// <summary>
        /// Create a storage for a given scope.
        /// </summary>
        /// <param name="scope">The scope of all references in this storage, determines the id prefix.</param>
        public DataReferenceStorage(DataScopes scope)
        {
            Scope = scope;
            TypePrefix = "" + DataScopeTools.ScopePrefixes[(int)scope];
        }

        /// <summary>
        /// Remove all references (cancelling their scheduled expiration).
        /// </summary>
        public void Dispose()
        {
            foreach (var x in Data.Values.ToList())
                x.Remove();
        }

        /// <summary>
        /// Add a reference to a data table.
        /// The table is stored as is (not copied), but the column definitions are cloned into the reference.
        /// </summary>
        /// <param name="data">The data table to get a reference to, must have columns.</param>
        /// <param name="timeToLiveInSeconds">The number of seconds that this data should live after creation or last use (minimum 10).</param>
        /// <returns>A reference to the data</returns>
        /// <exception cref="Exception">The <paramref name="data"/> has no columns.</exception>
        public TableDataReference Add(BaseTableData data, int timeToLiveInSeconds = 5 * 60)
        {
            var g = GetGuid();
            var c = Data;
            var d = new TableDataReference(Scope, g, data, timeToLiveInSeconds, () => c.TryRemove(g, out var _));
            c.TryAdd(g, d);
            return d;
        }

        /// <summary>
        /// Get the table data reference for a given id and renew its life time.
        /// Anything from the first '@' (used to address another session's storage) is ignored.
        /// </summary>
        /// <param name="dataRefId">The id of the data reference, may not be null.</param>
        /// <returns>The reference, or null if not found (or expired) or if it isn't a <see cref="TableDataReference"/>.</returns>
        public TableDataReference GetTable(String dataRefId)
        {
            var i = dataRefId.IndexOf('@');
            if (i > 0)
                dataRefId = dataRefId.Substring(0, i);
            if (!Data.TryGetValue(dataRefId, out var d))
                return null;
            d.Renew();
            return d as TableDataReference;
        }


        /// <summary>
        /// The scope of all references in this storage.
        /// </summary>
        public readonly DataScopes Scope;

        /// <summary>
        /// The single char prefix used for all ids in this storage (from <see cref="DataScopeTools.ScopePrefixes"/>).
        /// </summary>
        public readonly String TypePrefix;

        String GetGuid()
            => TypePrefix + CompactAsciiString.Secure.Encode((ulong)Interlocked.Increment(ref Id));

        static readonly long BaseTick = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;

        long Id = DateTime.UtcNow.Ticks - BaseTick;

        readonly ConcurrentDictionary<String, DataReference> Data = new ConcurrentDictionary<string, DataReference>(StringComparer.Ordinal);


        /// <summary>
        /// Enumerate over all live data references (safe to enumerate while references are added or removed).
        /// </summary>
        public IEnumerable<DataReference> AllReferences => Data.Values;

    }


}
