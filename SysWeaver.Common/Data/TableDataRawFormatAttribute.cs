using System;
using System.Linq;

namespace SysWeaver.Data
{
    /// <summary>
    /// Format hint
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class TableDataRawFormatAttribute : Attribute
    {
        public TableDataRawFormatAttribute(String value)
        {
            Value = value;
        }

        public TableDataRawFormatAttribute(TableDataFormats format, params Object[] options)
        {
            String opt = "";
            if ((options != null) && (options.Length > 0))
                opt = ";" + String.Join(';', options.Select(x => x?.ToString() ?? ""));
            Value = format + opt;
        }

        public readonly String Value;
    }


}



