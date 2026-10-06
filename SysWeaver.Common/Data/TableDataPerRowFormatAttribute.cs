namespace SysWeaver.Data
{
    /// <summary>
    /// The formatting and type is determined in the next column.
    /// The value of the next cell must be a text in the "TypeName|Format" format (the format is a raw format as produced by <see cref="TableDataRawFormatAttribute"/>).
    /// If the next cell is empty the value is formatted as a string.
    /// </summary>
    public class TableDataPerRowFormatAttribute : TableDataRawFormatAttribute
    {
        /// <summary>
        /// Format each value using the type and format specified in the next column.
        /// </summary>
        public TableDataPerRowFormatAttribute() : base(TableDataFormats.PerRowFormat)
        {
        }
    
    }


}



