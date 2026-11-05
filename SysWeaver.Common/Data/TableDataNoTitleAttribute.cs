using System;

namespace SysWeaver.Data
{
    /// <summary>
    /// Put on a member to tell any renderer to not show the column title in the table header (sets <see cref="TableDataColumnProps.NoTitle"/>).
    /// Use it for narrow columns that only show an icon (ex: a "📊" link to a chart), give the column a meaningful title / description
    /// (ex: using <see cref="TableDataTitleAttribute"/> and <see cref="TableDataDescAttribute"/> or an XML summary), since it's still used for
    /// tool tips, accessibility (screen readers), phone card labels, column menus and exports.
    /// </summary>
    /// <remarks>
    /// Sorting and filtering are not affected (they still apply when the column allows them, see <see cref="TableDataNoSortAttribute"/>).
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TableDataNoTitleAttribute : Attribute
    {
    }


}
