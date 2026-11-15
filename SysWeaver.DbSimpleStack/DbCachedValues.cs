using System;
using System.Collections.Generic;

namespace SysWeaver.Db
{
    /// <summary>
    /// A snapshot of cached database rows
    /// </summary>
    /// <typeparam name="K">The key type</typeparam>
    /// <typeparam name="T">The row type</typeparam>
    public sealed class DbCachedValues<K, T>  where T : class, new()
    {
        /// <inheritdoc />
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


        /// <summary>
        /// Create a snapshot
        /// </summary>
        /// <param name="values">The values</param>
        /// <param name="syncStart">Time stamp when the db read begun</param>
        /// <param name="syncEnd">Time stamp when the db read ended</param>
        public DbCachedValues(IReadOnlyDictionary<K, T> values, DateTime syncStart, DateTime syncEnd)
        {
            Values = values;
            SyncStart = syncStart;
            SyncEnd = syncEnd;
        }
    }

}