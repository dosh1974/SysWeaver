using System;
using SysWeaver.AI;

namespace SysWeaver.Data
{

    /// <summary>
    /// Represents a column of data
    /// </summary>
    public class TableDataBaseColumn
    {
#if DEBUG
        public override string ToString() => Name;
#endif//DEBUG
        /// <summary>
        /// The unique name/id of this column
        /// </summary>
        public String Name;
        
        /// <summary>
        /// The .NET type name of the data represented
        /// </summary>
        public String Type;
        
        /// <summary>
        /// Description of the data (shown as a tool tip)
        /// </summary>
        [OpenAiOptional]
        [EditAllowNull]
        [AutoTranslate(false)]
        [AutoTranslateContext("This is the tool tip description of a column in a table.")]
        [AutoTranslateContext("The name (id) of the column is \"{0}\"", nameof(Name))]
        public String Desc;

        public void CopyFrom(TableDataBaseColumn from)
        {
            Name = from.Name;
            Type = from.Type;
            Desc = from.Desc;
        }

        public TableDataBaseColumn Clone()
        {
            var r = new TableDataBaseColumn();
            r.CopyFrom(this);
            return r;
        }

    }



    /// <summary>
    /// Represents a column of data
    /// </summary>
    public class TableDataColumn : TableDataBaseColumn
    {
        /// <summary>
        /// Formatting hint (used when displaying the table), depends on Type etc.
        /// </summary>
        [OpenAiOptional]
        [EditAllowNull]
        public String Format;
        
        /// <summary>
        /// The column title (used when displaying the table, default is a cleaned up version of the Name).
        /// </summary>
        [OpenAiOptional]
        [EditAllowNull]
        [AutoTranslate(false)]
        [AutoTranslateContext("This is the title text (header) of a column in a table")]
        [AutoTranslateContext("The tool tip description for the title is: \"{0}\"", nameof(Desc))]
        public String Title;
        
        /// <summary>
        /// Column properties (flags)
        /// </summary>
        [OpenAiIgnore]
        public TableDataColumnProps Props;

        public void CopyFrom(TableDataColumn from)
        {
            base.CopyFrom(from);
            Format = from.Format;
            Title = from.Title;
            Props = from.Props;
        }

        public new TableDataColumn Clone()
        {
            var t = new TableDataColumn();
            t.CopyFrom(this);
            return t;
        }
    }




}
