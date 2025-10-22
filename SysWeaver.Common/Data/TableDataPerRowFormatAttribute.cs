namespace SysWeaver.Data
{
    /// <summary>
    /// The formatting and type is determined in the next column.
    /// The format of the next cell must be a text in the "TypeName|Format".
    /// </summary>
    public class TableDataPerRowFormatAttribute : TableDataRawFormatAttribute
    {
        public TableDataPerRowFormatAttribute() : base(TableDataFormats.PerRowFormat)
        {
        }
    
    }


}



