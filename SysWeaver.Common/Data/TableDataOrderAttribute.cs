using System;

namespace SysWeaver.Data
{
    /// <summary>
    /// Put on a member to control the column order when using the type in a data table.
    /// Columns are sorted by this value (ascending), members without the attribute have order 0.
    /// Members with the same order keep their declaration order.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataOrderAttribute : Attribute
    {
        /// <summary>
        /// Set the column order.
        /// </summary>
        /// <param name="order">The order, lower values are placed first</param>
        public TableDataOrderAttribute(int order)
        {
            Order = order;
        }

        /// <summary>
        /// The order, lower values are placed first
        /// </summary>
        public readonly int Order;
    }



    /// <summary>
    /// Put on a member to explicitly include (or exclude) it in a data table.
    /// </summary>
    /// <remarks>
    /// Note: Currently not read by any code in the framework, use <see cref="TableDataIgnoreAttribute"/> to exclude a member.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataIncludeAttribute : Attribute
    {
        /// <summary>
        /// Include (or exclude) a member.
        /// </summary>
        /// <param name="include">True to include the member</param>
        public TableDataIncludeAttribute(bool include = true)
        {
            Include = include;
        }

        /// <summary>
        /// True if the member should be included
        /// </summary>
        public readonly bool Include;
    }


    /// <summary>
    /// Put on a string member to control if (and with what weight) it's used when performing a text search on the rows of a data table.
    /// By default all string fields and properties (including non-public ones) are searched with a weight of 1.
    /// </summary>
    /// <remarks>
    /// Members with a weight of zero or less are excluded from the search, the remaining are ordered by descending weight.
    /// Has no effect on members that aren't of type <see cref="String"/>.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataSearchAttribute : Attribute
    {
        /// <summary>
        /// Enable or disable searching on this member.
        /// </summary>
        /// <param name="enable">True to search this member, false to exclude it from searches</param>
        /// <param name="weight">The weight to use if enabled</param>
        public TableDataSearchAttribute(bool enable, double weight = 1.0)
        {
            Weight = enable ? weight : 0;
        }

        /// <summary>
        /// Set the search weight of this member.
        /// </summary>
        /// <param name="weight">The weight, zero or less excludes the member from searches</param>
        public TableDataSearchAttribute(double weight = 1.0)
        {
            Weight = weight;
        }

        /// <summary>
        /// The search weight, zero or less excludes the member from searches
        /// </summary>
        public readonly double Weight;
    }


}



