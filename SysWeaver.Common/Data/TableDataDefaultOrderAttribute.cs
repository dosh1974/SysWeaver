using System;

namespace SysWeaver.Data
{
    /// <summary>
    /// Put on a row type to give its data tables a default sort order, used when a request has no sort order (<see cref="TableDataOrderRequest"/>.Order null or empty).
    /// The order is also sent to the client with the columns (<see cref="CommonTableData.DefaultOrder"/>), so a table can show which column it is sorted by.
    /// </summary>
    /// <example>
    /// <code>
    /// [TableDataDefaultOrder("-LastTime")]   // The latest first
    /// sealed class ProblemRow { ... }
    /// </code>
    /// </example>
    /// <remarks>
    /// Uses the request syntax: column names in priority order, a '-' prefix sorts descending. Unknown names are ignored.
    /// A text search still orders the result by rank.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = true, AllowMultiple = false)]
    public sealed class TableDataDefaultOrderAttribute : Attribute
    {
        /// <summary>
        /// Set the default sort order.
        /// </summary>
        /// <param name="order">Column names in priority order, a '-' prefix sorts descending (ex: "-LastActivity")</param>
        public TableDataDefaultOrderAttribute(params String[] order)
        {
            Order = order;
        }

        /// <summary>
        /// Column names in priority order, a '-' prefix sorts descending
        /// </summary>
        public readonly String[] Order;
    }
}
