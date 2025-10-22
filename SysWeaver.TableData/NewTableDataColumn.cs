using System;
using SysWeaver.AI;

namespace SysWeaver.Data
{
    /// <summary>
    /// Represents a new column
    /// </summary>
    public sealed class NewTableDataColumn : TableDataColumn
    {
        /// <summary>
        /// The expression to use for initializing this column.
        /// Only integers, Single, Double, 
        /// If null, the default value will be inserted.
        /// The value of other columns can be used as variables in the expression (by using the column Name), ex:
        /// "Min(Col1 + Col2, Col3 * Col4)".
        /// </summary>
        [EditMin(1)]
        public String Expression;

        /// <summary>
        /// If set, the new column is inserted before this column.
        /// </summary>
        [OpenAiOptional]
        [EditAllowNull]
        public String InsertBefore;

        /// <summary>
        /// If InsertBefore is null and this is set, the new column is inserted after this column.
        /// </summary>
        [OpenAiOptional]
        [EditAllowNull]
        public String InsertAfter;
    }




}
