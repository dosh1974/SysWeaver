using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SysWeaver.Data;

namespace SysWeaver.Data
{
    public sealed class DataReferenceStorage : IDisposable
    {
        public DataReferenceStorage(DataScopes scope)
        {
            Scope = scope;
            TypePrefix = "" + DataScopeTools.ScopePrefixes[(int)scope];
        }

        public void Dispose()
        {
            foreach (var x in Data.Values.ToList())
                x.Remove();
        }

        /// <summary>
        /// Add a reference to a data table
        /// </summary>
        /// <param name="data">The data table to get a reference to</param>
        /// <param name="timeToLiveInSeconds">The number of seconds that this data should live</param>
        /// <returns>A reference to the data</returns>
        public TableDataReference Add(BaseTableData data, int timeToLiveInSeconds = 5 * 60)
        {
            var g = GetGuid();
            var c = Data;
            var d = new TableDataReference(Scope, g, data, timeToLiveInSeconds, () => c.TryRemove(g, out var _));
            c.TryAdd(g, d);
            return d;
        }

        /// <summary>
        /// Get the table data reference for a given id
        /// </summary>
        /// <param name="dataRefId">The id of the data reference</param>
        /// <returns></returns>
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


        public readonly DataScopes Scope;
        public readonly String TypePrefix;

        String GetGuid()
            => TypePrefix + CompactAsciiString.Secure.Encode((ulong)Interlocked.Increment(ref Id));

        static readonly long BaseTick = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;

        long Id = DateTime.UtcNow.Ticks - BaseTick;

        readonly ConcurrentDictionary<String, DataReference> Data = new ConcurrentDictionary<string, DataReference>(StringComparer.Ordinal);


        /// <summary>
        /// Enumerate over all data references
        /// </summary>
        public IEnumerable<DataReference> AllReferences => Data.Values;

    }


}
