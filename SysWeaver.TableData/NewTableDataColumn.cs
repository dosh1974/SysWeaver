using System;
using SysWeaver.AI;

namespace SysWeaver.Data
{
    /// <summary>
    /// Describes a new (computed) column to add to a table, see <see cref="TableDataEdit.AddColumns(BaseTableData, NewTableDataColumn[])"/>.
    /// The inherited <see cref="TableDataBaseColumn.Name"/> must be unique and <see cref="TableDataBaseColumn.Type"/> must be a resolvable type name.
    /// If no <see cref="TableDataBaseColumn.Desc"/> is given the expression is used, if no <see cref="TableDataColumn.Title"/> is given it's derived from the name.
    /// </summary>
    public sealed class NewTableDataColumn : TableDataColumn
    {
        /// <summary>
        /// The expression used to compute the value of this column for each row.
        /// Only evaluated if the column type is supported by the expression evaluator (numeric types), else the default value is used.
        /// If null or empty, the default value of the column type will be inserted.
        /// The value of other columns (that can be converted to the column type) can be used as variables in the expression (by using the column Name), ex:
        /// "Min(Col1 + Col2, Col3 * Col4)".
        /// New columns defined earlier in the same operation may also be used.
        /// </summary>
        [EditMin(1)]
        public String Expression;

        /// <summary>
        /// If set, the new column is inserted before the column with this name.
        /// </summary>
        [AiOptional]
        [EditAllowNull]
        public String InsertBefore;

        /// <summary>
        /// If <see cref="InsertBefore"/> is null and this is set, the new column is inserted after the column with this name.
        /// If neither is set the column is appended last.
        /// </summary>
        [AiOptional]
        [EditAllowNull]
        public String InsertAfter;
    }




}
