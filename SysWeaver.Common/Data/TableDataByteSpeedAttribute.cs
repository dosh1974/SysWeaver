using System;

namespace SysWeaver.Data
{
    /// <summary>
    /// Format the value using bytes/s, KB/s, MB/s and so on
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class TableDataByteSpeedAttribute : TableDataRawFormatAttribute
    {
        /// <summary>
        /// Format the value using bytes/s, KB/s, MB/s and so on
        /// </summary>
        public TableDataByteSpeedAttribute() : base(TableDataFormats.ByteSpeed)
        {
        }
    }


}
