using System;

namespace SysWeaver.Data
{
    /// <summary>
    /// Format valus as:
    /// A clickable link.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class TableDataUrlAttribute : TableDataRawFormatAttribute
    {


        /// <summary>
        /// A clickable link.
        /// </summary>
        /// <param name="textFormat">
        /// {0} = This value. 
        /// {1} = Next value (must exist). 
        /// </param>
        /// <param name="urlFormat">
        /// {0} = This value. 
        /// {1} = Next value (must exist). 
        /// {2} = The text (after formatting). 
        /// The target for the link can (optionally) be controlled by prefixing (the evaluated) url with:
        /// '+' open in a new tab: "_blank" (default).
        /// '*' open in same frame: "_self".
        /// '^' open in same window: "_top".
        /// '-' open in parent frame: "_parent".
        /// </param>
        /// <param name="titleFormat">
        /// {0} = This value. 
        /// {1} = Next value (must exist). 
        /// {2} = The text (after formatting). 
        /// {3} = The url (after formatting). 
        /// </param>
        public TableDataUrlAttribute(String textFormat = "{0}", String urlFormat = "{2}", String titleFormat = "Click to open \"{3}\".") : base(TableDataFormats.Url, textFormat ?? "{0}", urlFormat ?? "{2}", titleFormat ?? "Click to open \"{3}\".")
        {
        }
    }




    /// <summary>
    /// Format valus as a web color
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class TableDataWebColorAttribute : TableDataRawFormatAttribute
    {
        /// <summary>
        /// A web color.
        /// </summary>
        /// <param name="valueFormat">How to format the value.
        /// {0} = This value. 
        /// {1} = Next value (must exist). 
        /// </param>
        /// <param name="titleFormat">How to format the title
        /// {0} = This value. 
        /// {1} = Next value (must exist). 
        /// {2} = Formatted value.
        /// </param>
        public TableDataWebColorAttribute(String valueFormat = "{0}", String titleFormat = null) 
            
            : base(TableDataFormats.WebColor, valueFormat ?? "{0}", titleFormat)
        {
        }
    }


}



