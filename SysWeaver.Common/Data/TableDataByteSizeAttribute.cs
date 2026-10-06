using System;

namespace SysWeaver.Data
{
    /// <summary>
    /// Format the value using bytes, kb, Mb and so on
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class TableDataByteSizeAttribute : TableDataRawFormatAttribute
    {

        /// <summary>
        /// A shared instance (useful when a format attribute is needed in code, ex: for dynamically created columns)
        /// </summary>
        public static readonly TableDataByteSizeAttribute Instance = new TableDataByteSizeAttribute();

        /// <summary>
        /// Format the value using bytes, kb, Mb and so on
        /// </summary>
        public TableDataByteSizeAttribute() : base(TableDataFormats.ByteSize) 
        { 
        }
    }

    /// <summary>
    /// Format a DateTime value as the dynamic measure time since the time
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class TableDataUptimeAttribute : TableDataRawFormatAttribute
    {

        /// <summary>
        /// A shared instance (useful when a format attribute is needed in code, ex: for dynamically created columns)
        /// </summary>
        public static readonly TableDataUptimeAttribute Instance = new TableDataUptimeAttribute();

        /// <summary>
        /// Format a DateTime value as the dynamic measure time since the time
        /// </summary>
        public TableDataUptimeAttribute() : base(TableDataFormats.Uptime)
        {
        }
    }
    

}
