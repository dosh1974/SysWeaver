using System;
using System.Linq;

namespace SysWeaver.Data
{
    /// <summary>
    /// Format hint, the value is copied to <see cref="TableDataColumn.Format"/> and interpreted by the renderer (the web client).
    /// Base class of all the table data format attributes.
    /// </summary>
    /// <remarks>
    /// The format string is the name of a <see cref="TableDataFormats"/> value followed by the options, all separated with ';'.
    /// Since ';' is used as the separator, options can't contain a ';'.
    /// Only one format attribute can be applied to a member.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class TableDataRawFormatAttribute : Attribute
    {
        /// <summary>
        /// Use a raw format string.
        /// </summary>
        /// <param name="value">The format string, ex: "Number;2"</param>
        public TableDataRawFormatAttribute(String value)
        {
            Value = value;
        }

        /// <summary>
        /// Build a format string from a format and options.
        /// </summary>
        /// <param name="format">The format</param>
        /// <param name="options">The format specific options (converted using ToString, null is converted to an empty string), see <see cref="TableDataFormats"/> for details</param>
        public TableDataRawFormatAttribute(TableDataFormats format, params Object[] options)
        {
            String opt = "";
            if ((options != null) && (options.Length > 0))
                opt = ";" + String.Join(';', options.Select(x => x?.ToString() ?? ""));
            Value = format + opt;
        }

        /// <summary>
        /// The format string, ex: "Number;2;{0};Raw: {2};True"
        /// </summary>
        public readonly String Value;
    }


}



