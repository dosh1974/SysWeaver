using System;

namespace SysWeaver.Data
{
    /// <summary>
    /// Format the value using bytes, kb, Mb and so on
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class TableDataByteSizeAttribute : TableDataRawFormatAttribute
    {

        public static readonly TableDataByteSizeAttribute Instance = new TableDataByteSizeAttribute();

        /// <summary>
        /// Format the value using bytes, kb, Mb and so on
        /// </summary>
        public TableDataByteSizeAttribute() : base(TableDataFormats.ByteSize) 
        { 
        }
    }


}
