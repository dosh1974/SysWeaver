using System;
using System.Collections.Generic;

namespace SysWeaver.Data
{
    /// <summary>
    /// Action buttons with an optional confirmation question and an optional per row visibility (an extended <see cref="TableDataActionsAttribute"/>).
    /// </summary>
    /// <remarks>
    /// Each button is described by 6 strings: "text", "title", "url", "icon", "confirm" and "visible", encoded as "text|title|url|icon|confirm|visible",
    /// so the strings can't contain '|' (or ';', the format separator).
    /// All strings can use {0} (the value; its first part when it is a comma separated string), {1} (the next value) and {2} to {x} (the following comma separated parts of the value).
    /// <para>"confirm": the question to ask (in a confirmation dialog) before the action is performed, ex: "Kill the process?".
    /// A line break separates the question (the dialog title) from an explanation (the dialog text), ex: "Force a password reset for {2}?\nThey must choose
    /// a new password at the next sign in.". With an explanation the server text is all the dialog says (no generic "This cannot be undone." warning),
    /// so say it in the explanation when it is true.
    /// An empty string (or null) means not specified: the renderer decides (the web client asks before actions that look destructive, ex: "Delete", "Kill").
    /// Use <see cref="NoConfirm"/> to never ask.</para>
    /// <para>"visible": an empty string (or null) means always visible, else the action is hidden on a row when the formatted text is
    /// empty, "0", "false" or "no" (case insensitive), ex: "{2}" with a member value like "1234,0" (hidden) or "1234,1" (visible).</para>
    /// Renderers that only know <see cref="TableDataActionsAttribute"/> ignore the extra parts (the action is then always shown and the renderer decides about confirmations).
    /// An invalid number of button strings throws an <see cref="ArgumentException"/> when the attribute is instantiated (i.e. when it's read using reflection).
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class TableDataActionsExAttribute : TableDataRawFormatAttribute
    {
        /// <summary>
        /// Use as the "confirm" string to never ask for a confirmation (even if the action looks destructive).
        /// </summary>
        public const String NoConfirm = "-";

        static object[] Parse(String[] buttons)
        {
            List<object> res = new List<object>();
            if (buttons != null)
            {
                var bl = buttons.Length;
                if ((bl % 6) != 0)
                    throw new ArgumentException("The buttons must contains a multiple of 6 strings, one for text, title, url, icon, confirm and visible", nameof(buttons));
                for (int i = 0; i < bl; i += 6)
                    res.Add(String.Join('|', buttons[i] ?? "", buttons[i + 1] ?? "", buttons[i + 2] ?? "", buttons[i + 3] ?? "", buttons[i + 4] ?? "", buttons[i + 5] ?? ""));
            }
            return res.ToArray();
        }

        /// <summary>
        /// Action buttons with an optional confirmation question and an optional per row visibility.
        /// </summary>
        /// <param name="buttons">An array of buttons, 6 strings per button following a: "text", "title", "url", "icon", "confirm" and "visible" pattern.
        /// "url": the url of the get request that will be performed on click, if it start's with a '@' the url will be opened in a new tab, if it start's with a '&amp;' the url will be opened in the same tab.
        /// "icon": an icon class name or url.
        /// "confirm": the confirmation question, empty = the renderer decides, <see cref="NoConfirm"/> = never ask.
        /// "visible": empty = always visible, else hidden when the formatted text is empty, "0", "false" or "no".</param>
        /// <exception cref="ArgumentException">The number of strings in <paramref name="buttons"/> isn't a multiple of 6</exception>
        public TableDataActionsExAttribute(params String[] buttons)
            :
            base(TableDataFormats.Actions, Parse(buttons))
        {
        }

    }


}
