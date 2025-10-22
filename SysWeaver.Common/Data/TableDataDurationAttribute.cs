using System;

namespace SysWeaver.Data
{
    /// <summary>
    /// Format valus as as duration (time span, integer or float)
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class TableDataDurationAttribute : TableDataRawFormatAttribute
    {
        /// <summary>
        /// Format value.
        /// </summary>
        /// <param name="replaceZeroWith">Replace exact zero with this text string, null to write zero as is</param>
        /// <param name="textFormat">
        /// {0} = Value.
        /// {1} = Next value (must exist). 
        /// </param>
        /// <param name="titleFormat">
        /// {0} = Formatted value.
        /// {1} = Next value (must exist). 
        /// {2} = Value before formatting.
        /// </param>
        /// <param name="copyOnClick">Copy the original value to the clipboard on click.</param>
        public TableDataDurationAttribute(String replaceZeroWith = null, String textFormat = "{0}", String titleFormat = "Raw: {2}", bool copyOnClick = false)
            : base(TableDataFormats.Duration, textFormat ?? "{0}", titleFormat ?? "Raw: {2}", copyOnClick ? "{0}" : null, replaceZeroWith)
        {
        }


        /// <summary>
        /// Format value.
        /// </summary>
        /// <param name="replaceZeroWith">Replace exact zero with this text string, null to write zero as is</param>
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
        /// </param>
        public TableDataDurationAttribute(String replaceZeroWith, String textFormat, String titleFormat, String copyOnClickFormat)
            : base(TableDataFormats.Duration, textFormat ?? "{0}", titleFormat ?? "Raw: {2}", copyOnClickFormat == "" ? "{2}" : copyOnClickFormat, replaceZeroWith)
        {
        }
    }




    /// <summary>
    /// Format the text as json.
    /// Will show a capped version of the text.
    /// Will show the beutified json on click.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class TableDataJsonAttribute : TableDataRawFormatAttribute
    {
        /// <summary>
        /// Format the text as json.
        /// Will show a capped version of the text.
        /// Will show the beutified json on click.
        /// </summary>
        /// <param name="maxLength">Maximum number of chars to show</param>
        /// <param name="titleFormat">
        /// {0} = Value.
        /// {1} = Formatted value.
        /// {2} = Capped value.
        /// </param>
        /// <param name="copyOnClick">Copy the original value to the clipboard on click (if not null).
        /// {0} = Value.
        /// {1} = Formatted value.
        /// {2} = Capped value.
        /// </param>
        public TableDataJsonAttribute(int maxLength = 30, String titleFormat = "{1}", String copyOnClick = "{0}")
            : base(TableDataFormats.Json, maxLength > 0 ? maxLength : 30, titleFormat ?? "{1}", copyOnClick)
        {
        }
    }

    /// <summary>
    /// Format some long multiline text.
    /// Will show a capped version of the text.
    /// Will show the full version of the text on click.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class TableDataTextAttribute : TableDataRawFormatAttribute
    {
        /// <summary>
        /// Format some long multiline text.
        /// Will show a capped version of the text.
        /// Will show the full version of the text on click.
        /// </summary>
        /// <param name="maxLength">Maximum number of chars to show</param>
        /// <param name="titleFormat">
        /// {0} = Value.
        /// {1} = Capped value.
        /// </param>
        /// <param name="copyOnClick">Copy the original value to the clipboard on click (if not null).
        /// {0} = Value.
        /// {1} = Capped value.
        /// </param>
        public TableDataTextAttribute(int maxLength = 30, String titleFormat = "{0}", String copyOnClick = "{0}")
            : base(TableDataFormats.Text, maxLength > 0 ? maxLength : 30, titleFormat ?? "{0}", copyOnClick)
        {
        }
    }


    /// <summary>
    /// Format some long multiline Mark Down (MD) text.
    /// Will show a capped version of the text (not formatted using MD).
    /// Will show the full version of the text on click.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class TableDataMdAttribute : TableDataRawFormatAttribute
    {
        /// <summary>
        /// Format some long multiline Mark Down (MD) text.
        /// Will show a capped version of the text (not formatted using MD).
        /// Will show the full version of the text on click.
        /// </summary>
        /// <param name="maxLength">Maximum number of chars to show</param>
        /// <param name="titleFormat">
        /// {0} = Value.
        /// {1} = Capped value.
        /// </param>
        /// <param name="copyOnClick">Copy the original value to the clipboard on click (if not null).
        /// {0} = Value.
        /// {1} = Capped value.
        /// </param>
        public TableDataMdAttribute(int maxLength = 30, String titleFormat = "{0}", String copyOnClick = "{0}")
            : base(TableDataFormats.MD, maxLength > 0 ? maxLength : 30, titleFormat ?? "{0}", copyOnClick)
        {
        }
    }


}



