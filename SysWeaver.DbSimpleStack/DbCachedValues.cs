using System;
using System.Collections.Generic;

namespace SysWeaver.Db
{
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

}