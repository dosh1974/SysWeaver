using System;

namespace SysWeaver.Data
{
    /// <summary>
    /// Add a boolean toggle button.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class TableDataBooleanToggleAttribute : TableDataRawFormatAttribute
    {
        /// <summary>
        /// Add a boolean toggle button.
        /// </summary>
        /// <param name="toggleApiUrl">The get request to perform to toggle the value.
        /// {0} is the current value.
        /// {1} is the next value.
        /// Optionally {2} to {x} is populated from the value if it's a comma separated string.
        /// </param>
        /// <param name="trueText">The text to display when the value is true.
        /// {0} is the current value.
        /// {1} is the next value.
        /// Optionally {2} to {x} is populated from the value if it's a comma separated string.
        /// </param>
        /// <param name="falseText">The text to display when the value is false.
        /// {0} is the current value.
        /// {1} is the next value.
        /// Optionally {2} to {x} is populated from the value if it's a comma separated string.
        /// </param>
        /// <param name="trueTitle">The title (tooltip) when the value is true.
        /// {0} is the current value.
        /// {1} is the next value.
        /// Optionally {2} to {x} is populated from the value if it's a comma separated string.
        /// </param>
        /// <param name="falseTitle">The title (tooltip) when the value is false.
        /// {0} is the current value.
        /// {1} is the next value.
        /// Optionally {2} to {x} is populated from the value if it's a comma separated string.
        /// </param>
        public TableDataBooleanToggleAttribute(String toggleApiUrl, String trueText = "True", String falseText = "False", String trueTitle = "Click to uncheck", String falseTitle = "Click to check") 
            : 
            base(TableDataFormats.Toggle,
                trueText, falseText, trueTitle, falseTitle, toggleApiUrl)
        {
        }
    }


}
