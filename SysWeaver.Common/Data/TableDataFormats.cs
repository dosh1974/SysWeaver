namespace SysWeaver.Data
{
    /// <summary>
    /// The built in formats that can be used in a <see cref="TableDataColumn.Format"/> string.
    /// </summary>
    /// <remarks>
    /// The format string is the name of the format followed by the arguments, separated by ';', ex: "Number;2;{0}%".
    /// The name (not the numeric value) is used by the web client, so names must not be changed.
    /// Typically created using one of the attributes derived from <see cref="TableDataRawFormatAttribute"/>.
    /// </remarks>
    public enum TableDataFormats
    {
        /// <summary>
        /// First argument is the text formatting:
        /// {0} = Value.
        /// {1} = Next value (must exist). 
        /// The second argument is the title formatting. 
        /// {0} = Formatted value.
        /// {1} = Next value (must exist). 
        /// {2} = Value before formatting.
        /// The third argument is the click to copy formatting (with the same arguments as the title), if missing the value before formatting is copied, if empty nothing is copied.
        /// </summary>
        Default = 0,
        /// <summary>
        /// The first argument is the number of decimals. 
        /// The second argument is the text formatting.
        /// {0} = Formatted value (decimals and thousands separator applied)
        /// {1} = Next value (must exist). 
        /// {2} = Value before formatting.
        /// The third argument is the title formatting:
        /// {0} = Formatted value (decimals and thousands separator applied)
        /// {1} = Next value (must exist). 
        /// {2} = Value before formatting.
        /// {3} = The text (after formatting). 
        /// If the fourth argument is true, the original value will be copied to the clipboard on click.
        /// </summary>
        Number,
        /// <summary>
        /// First argument is the string format of the text to display where: 
        /// {0} = This value. 
        /// {1} = Next value (must exist). 
        /// Second argument is the string format of the url where: 
        /// {0} = This value. 
        /// {1} = Next value (must exist). 
        /// {2} = The text (after formatting). 
        /// Third argument is the string format of the title where: 
        /// {0} = This value. 
        /// {1} = Next value (must exist). 
        /// {2} = The text (after formatting). 
        /// {3} = The url (after formatting). 
        /// </summary>
        Url,

        /// <summary>
        /// List of tags separated by a comma, ex: "Banana, Apple, Orange". 
        /// An optional value can be present using a colon, ex: "Banana:Yellow, Apple:Green, Orange:Orange". 
        /// First argument is the value formatting: 
        /// {0} = The value, ex: "Banana:Yellow", "Apple:Green", "Orange:Orange". 
        /// {1} = Value without optional, ex "Banana", "Apple", "Orange". 
        /// {2} = Optional, ex: "Yellow", "Green", "Orange". 
        /// Second argument is the title formatting: 
        /// {0} = The value, ex: "Banana:Yellow", "Apple:Green", "Orange:Orange". 
        /// {1} = Value without optional, ex "Banana", "Apple", "Orange". 
        /// {2} = Optional, ex: "Yellow", "Green", "Orange". 
        /// {3} = The text (after formatting). 
        /// Third argument is the click to copy formatting: 
        /// {0} = The value, ex: "Banana:Yellow", "Apple:Green", "Orange:Orange". 
        /// {1} = Value without optional, ex "Banana", "Apple", "Orange". 
        /// {2} = Optional, ex: "Yellow", "Green", "Orange". 
        /// {3} = The text (after formatting). 
        /// {4} = The title (after formatting). 
        /// </summary>
        Tags,

        /// <summary>
        /// The type and format is found in the next column instead of in the header, the next column value must be "TypeName|Format"
        /// </summary>
        PerRowFormat,

        /// <summary>
        /// Format the value using bytes, kb, Mb and so on
        /// </summary>
        ByteSize,

        /// <summary>
        /// Format the value using bytes/s, kb/s, Mb/s and so on
        /// </summary>
        ByteSpeed,

        /// <summary>
        /// Add a boolean toggle button.
        /// First argument is the true text.
        /// {0} is the current value.
        /// {1} is the next value.
        /// Optionally {2} to {x} is populated from the value if it's a comma separated string.
        /// Second argument is the false text.
        /// {0} is the current value.
        /// {1} is the next value.
        /// Optionally {2} to {x} is populated from the value if it's a comma separated string.
        /// Third argument is the true title.
        /// {0} is the current value.
        /// {1} is the next value.
        /// Optionally {2} to {x} is populated from the value if it's a comma separated string.
        /// Fourth argument is the false title.
        /// {0} is the current value.
        /// {1} is the next value.
        /// Optionally {2} to {x} is populated from the value if it's a comma separated string.
        /// Fifth argument is the toggle api call.
        /// {0} is the current value.
        /// {1} is the next value.
        /// Optionally {2} to {x} is populated from the value if it's a comma separated string.
        /// </summary>
        Toggle,

        /// <summary>
        /// Action buttons.
        /// Each argument is a button, the button is a string with the following format:
        /// text|title|getUri|icon
        /// {0} is the current value.
        /// {1} is the next value.
        /// Optionally {2} to {x} is populated from the value if it's a comma separated string.
        /// </summary>
        Actions,


        /// <summary>
        /// First argument is the string format of the image source url where: 
        /// {0} = This value. 
        /// {1} = Next value (must exist). 
        /// Second argument is the string format of the url where: 
        /// {0} = This value. 
        /// {1} = Next value (must exist). 
        /// {2} = The image source url (after formatting). 
        /// Third argument is the string format of the title where: 
        /// {0} = This value. 
        /// {1} = Next value (must exist). 
        /// {2} = The image source url (after formatting). 
        /// {3} = The url (after formatting).
        /// Fourth and fifth arguments are the max width and max height of the image (ignored if zero or less).
        /// The sixth argument is a prefix to add to relative image urls.
        /// </summary>
        Img,

        /// <summary>
        /// Format as duration.
        /// The first and second arguments are the text and title formatting, the third the click to copy formatting and the fourth a text to replace an exact zero with.
        /// </summary>
        Duration,

        /// <summary>
        /// Format as json, only the first line (or max length) is shown.
        /// Full text on hover (and click).
        /// </summary>
        Json,

        /// <summary>
        /// Format as multiline text, only the first line (or max length) is shown.
        /// </summary>
        Text,

        /// <summary>
        /// Format as multiline mark down, only the first line (or max length) is shown.
        /// </summary>
        MD,

        /// <summary>
        /// Format a DateTime value as the dynamic measure time since the time
        /// </summary>
        Uptime,

        /// <summary>
        /// Format a string value as a web color
        /// </summary>
        WebColor,

    }


}
