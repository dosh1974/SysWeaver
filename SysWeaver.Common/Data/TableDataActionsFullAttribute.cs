using System;
using System.Collections.Generic;

namespace SysWeaver.Data
{
    /// <summary>
    /// Action buttons with an optional confirmation question, an optional per row visibility and an optional done text (an extended
    /// <see cref="TableDataActionsExAttribute"/>).
    /// </summary>
    /// <remarks>
    /// Each button is described by 7 strings: "text", "title", "url", "icon", "confirm", "visible" and "done", encoded as
    /// "text|title|url|icon|confirm|visible|done", so the strings can't contain '|' (or ';', the format separator).
    /// "confirm" and "visible" are as in <see cref="TableDataActionsExAttribute"/>, and all strings can use the same {0}, {1}, {2} .. {x}.
    /// <para>"done": the text the web client shows after the action succeeded (the result note in the row and the announcement), instead of
    /// its generic "&lt;text&gt;: done (&lt;row&gt;)", ex: "{2} must choose a new password at the next sign in". An empty string (or null) means
    /// the generic text. A done text that the request's own outcome carries still wins.</para>
    /// <para>The encoded format has an 8th part per button, "final": "final" when the button's <see cref="TableDataActionFlags"/> include
    /// <see cref="TableDataActionFlags.Final"/> (the action ends or removes the row's item, ex: Delete, Kill, Withdraw invitation), else empty. Every button of
    /// this attribute has the part, so the web client knows what each action does from the server and never guesses it from the words (G47);
    /// set the flags with <see cref="TableDataActionsFullAttribute(TableDataActionFlags[], String[])"/>. Actions without the part (<see cref="TableDataActionsAttribute"/>,
    /// <see cref="TableDataActionsExAttribute"/>) are not final: the table re-reads the row after them, as it always did.</para>
    /// Renderers that only know <see cref="TableDataActionsExAttribute"/> ignore the extra parts.
    /// An invalid number of button strings throws an <see cref="ArgumentException"/> when the attribute is instantiated (i.e. when it's read using reflection).
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class TableDataActionsFullAttribute : TableDataRawFormatAttribute
    {
        /// <summary>
        /// The number of strings per button.
        /// </summary>
        public const int StringsPerButton = 7;

        /// <summary>
        /// The number of encoded parts per button in <see cref="TableDataRawFormatAttribute.Value"/>: the <see cref="StringsPerButton"/> strings and "final".
        /// </summary>
        public const int PartsPerButton = 8;

        /// <summary>
        /// The 8th part of a button whose action is final (<see cref="TableDataActionFlags.Final"/>), else the part is empty.
        /// </summary>
        public const String FinalPart = "final";

        static object[] Parse(TableDataActionFlags[] flags, String[] buttons)
        {
            List<object> res = new List<object>();
            var fl = flags?.Length ?? 0;
            if (buttons != null)
            {
                var bl = buttons.Length;
                if ((bl % StringsPerButton) != 0)
                    throw new ArgumentException("The buttons must contains a multiple of 7 strings, one for text, title, url, icon, confirm, visible and done", nameof(buttons));
                if (fl > (bl / StringsPerButton))
                    throw new ArgumentException("There are more action flags (" + fl + ") than buttons (" + (bl / StringsPerButton) + ")", nameof(flags));
                for (int i = 0, b = 0; i < bl; i += StringsPerButton, ++b)
                {
                    var final = (b < fl) && ((flags[b] & TableDataActionFlags.Final) != 0);
                    res.Add(String.Join('|', buttons[i] ?? "", buttons[i + 1] ?? "", buttons[i + 2] ?? "", buttons[i + 3] ?? "", buttons[i + 4] ?? "", buttons[i + 5] ?? "", buttons[i + 6] ?? "", final ? FinalPart : ""));
                }
            }
            else if (fl > 0)
                throw new ArgumentException("There are action flags but no buttons", nameof(flags));
            return res.ToArray();
        }

        /// <summary>
        /// Action buttons with an optional confirmation question, an optional per row visibility and an optional done text.
        /// </summary>
        /// <param name="buttons">An array of buttons, 7 strings per button following a: "text", "title", "url", "icon", "confirm", "visible" and "done" pattern.
        /// "url": the url of the get request that will be performed on click, if it start's with a '@' the url will be opened in a new tab, if it start's with a '&amp;' the url will be opened in the same tab.
        /// "icon": an icon class name or url.
        /// "confirm": the confirmation question, empty = the renderer decides, <see cref="TableDataActionsExAttribute.NoConfirm"/> = never ask.
        /// "visible": empty = always visible, else hidden when the formatted text is empty, "0", "false" or "no".
        /// "done": the text shown after the action succeeded, empty = the generic text.</param>
        /// <exception cref="ArgumentException">The number of strings in <paramref name="buttons"/> isn't a multiple of 7</exception>
        public TableDataActionsFullAttribute(params String[] buttons)
            :
            base(TableDataFormats.Actions, Parse(null, buttons))
        {
        }

        /// <summary>
        /// Action buttons with an optional confirmation question, an optional per row visibility, an optional done text and declared flags
        /// (ex: which actions are final).
        /// </summary>
        /// <param name="flags">The flags of each button, in the order of the buttons, ex: new[] { TableDataActionFlags.None, TableDataActionFlags.Final }
        /// for "Edit" and "Delete". Buttons after the end of the array have no flags (<see cref="TableDataActionFlags.None"/>).</param>
        /// <param name="buttons">An array of buttons, 7 strings per button, as in <see cref="TableDataActionsFullAttribute(String[])"/>.</param>
        /// <exception cref="ArgumentException">The number of strings in <paramref name="buttons"/> isn't a multiple of 7, or there are more flags than buttons</exception>
        public TableDataActionsFullAttribute(TableDataActionFlags[] flags, params String[] buttons)
            :
            base(TableDataFormats.Actions, Parse(flags, buttons))
        {
        }

    }


}
