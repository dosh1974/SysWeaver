using System;

namespace SysWeaver.Data
{
    /// <summary>
    /// Format valus as:
    /// List of tags separated by a comma, ex: "Banana, Apple, Orange".
    /// An optional value can be present using a colon, ex: "Banana:Yellow, Apple:Green, Orange:Orange".
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class TableDataTagsAttribute : TableDataRawFormatAttribute
    {

        /// <summary>
        /// List of tags separated by a comma, ex: "Banana, Apple, Orange". 
        /// An optional value can be present using a colon, ex: "Banana:Yellow, Apple:Green, Orange:Orange".
        /// All '¤' characters will be replaced to ',' after splitting.
        /// </summary>
        /// <param name="textFormat">
        /// {0} = The value, ex: "Banana:Yellow", "Apple:Green", "Orange:Orange". 
        /// {1} = Value without optional, ex "Banana", "Apple", "Orange". 
        /// {2} = Optional, ex: "Yellow", "Green", "Orange". 
        /// </param>
        /// <param name="titleFormat">
        /// {0} = The value, ex: "Banana:Yellow", "Apple:Green", "Orange:Orange". 
        /// {1} = Value without optional, ex "Banana", "Apple", "Orange". 
        /// {2} = Optional, ex: "Yellow", "Green", "Orange" or text after formatting if no optional.
        /// {3} = The text (after formatting). 
        /// </param>
        /// <param name="copyFormat">
        /// {0} = The value, ex: "Banana:Yellow", "Apple:Green", "Orange:Orange". 
        /// {1} = Value without optional, ex "Banana", "Apple", "Orange". 
        /// {2} = Optional, ex: "Yellow", "Green", "Orange". 
        /// {3} = The text (after formatting). 
        /// {4} = The title (after formatting). 
        /// </param>
        /// <param name="copyOnClick">Copy all tags (raw value) to the clipboard on click.</param>
        public TableDataTagsAttribute(String textFormat = "{1}", String titleFormat = "{2}", String copyFormat = null, bool copyOnClick = false) : base(TableDataFormats.Tags, textFormat ?? "{1}", titleFormat ?? "{2}", copyFormat ?? "", copyOnClick)
        {
        }
    }


}



