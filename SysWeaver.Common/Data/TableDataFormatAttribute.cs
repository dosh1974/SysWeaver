using System;

namespace SysWeaver.Data
{
    /// <summary>
    /// Format value.
    /// </summary>
    /// <remarks>
    /// The copy on click format is applied with the formatted value as {0}, so the boolean copyOnClick overloads copy the formatted value (not the value before formatting).
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class TableDataFormatAttribute : TableDataRawFormatAttribute
    {
        /// <summary>
        /// Format value.
        /// </summary>
        /// <param name="textFormat">
        /// {0} = Value.
        /// {1} = Next value (must exist).
        /// </param>
        /// <param name="titleFormat">
        /// {0} = Formatted value.
        /// {1} = Next value (must exist).
        /// {2} = Value before formatting.
        /// </param>
        /// <param name="copyOnClick">Copy the formatted value to the clipboard on click.</param>
        public TableDataFormatAttribute(String textFormat = "{0}", String titleFormat = "Raw: {2}", bool copyOnClick = false)
            : base(TableDataFormats.Default, textFormat ?? "{0}", titleFormat ?? "Raw: {2}", copyOnClick ? "{0}" : null)
        {
        }


        /// <summary>
        /// Format value.
        /// </summary>
        /// <param name="textFormat">
        /// {0} = Value.
        /// {1} = Next value (must exist). 
        /// </param>
        /// <param name="titleFormat">
        /// {0} = Formatted value.
        /// {1} = Next value (must exist). 
        /// {2} = Value before formatting.
        /// </param>
        /// <param name="copyOnClickFormat">Copy the value to the clipboard on click, using this string formatter.
        /// {0} = Formatted value.
        /// {1} = Next value (must exist).
        /// {2} = Value before formatting.
        /// An empty string copies the value before formatting ("{2}"), null disables copy on click.
        /// </param>
        public TableDataFormatAttribute(String textFormat, String titleFormat, String copyOnClickFormat)
            : base(TableDataFormats.Default, textFormat ?? "{0}", titleFormat ?? "Raw: {2}", copyOnClickFormat == "" ? "{2}" : copyOnClickFormat)
        {
        }


        /// <summary>
        /// Format value using a specific formatter.
        /// </summary>
        /// <param name="format">The specific format to use</param>
        /// <param name="textFormat">
        /// {0} = Value.
        /// {1} = Next value (must exist). 
        /// </param>
        /// <param name="titleFormat">
        /// {0} = Formatted value.
        /// {1} = Next value (must exist). 
        /// {2} = Value before formatting.
        /// </param>
        /// <param name="copyOnClick">Copy the formatted value to the clipboard on click.</param>
        public TableDataFormatAttribute(TableDataFormats format, String textFormat = "{0}", String titleFormat = "Raw: {2}", bool copyOnClick = false)
            : base(format, textFormat ?? "{0}", titleFormat ?? "Raw: {2}", copyOnClick ? "{0}" : null)
        {
        }


        /// <summary>
        /// Format value using a specific formatter.
        /// </summary>
        /// <param name="format">The specific format to use</param>
        /// <param name="textFormat">
        /// {0} = Value.
        /// {1} = Next value (must exist). 
        /// </param>
        /// <param name="titleFormat">
        /// {0} = Formatted value.
        /// {1} = Next value (must exist). 
        /// {2} = Value before formatting.
        /// </param>
        /// <param name="copyOnClickFormat">Copy the value to the clipboard on click, using this string formatter.
        /// {0} = Formatted value.
        /// {1} = Next value (must exist).
        /// {2} = Value before formatting.
        /// An empty string copies the value before formatting ("{2}"), null disables copy on click.
        /// </param>
        public TableDataFormatAttribute(TableDataFormats format, String textFormat, String titleFormat, String copyOnClickFormat)
            : base(format, textFormat ?? "{0}", titleFormat ?? "Raw: {2}", copyOnClickFormat == "" ? "{2}" : copyOnClickFormat)
        {
        }


    }


}



