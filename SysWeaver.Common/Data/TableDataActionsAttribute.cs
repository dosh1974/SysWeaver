using System;
using System.Collections.Generic;

namespace SysWeaver.Data
{
    /// <summary>
    /// Action buttons.
    /// </summary>
    /// <remarks>
    /// Each button is encoded as "text|title|url|icon", so the strings can't contain '|' (or ';', the format separator).
    /// The formats can use {0} (the current value), {1} (the next value) and {2} to {x} (populated from the value if it's a comma separated string).
    /// An invalid number of button strings throws an <see cref="ArgumentException"/> when the attribute is instantiated (i.e. when it's read using reflection).
    /// Use <see cref="TableDataActionsExAttribute"/> to specify a confirmation question per action and/or to show an action only on some rows.
    /// <para>The outcome of the API an action calls: the client shows a result once, by the row. Return true (or a value) when the work was done. When it can't be done
    /// (the row is gone, already used, already ended, ...), throw a <see cref="SysWeaver.Net.HttpResponseException"/> with 409 (Conflict) and a reason for the user, ex:
    /// "The invitation could not be withdrawn: it was already used, has expired or was withdrawn". The reason is shown in the row ("not done: reason"),
    /// translated, and it is not counted as a failure of the server (Problems). Never return a bare false for "not done": a false result is shown as
    /// "not done" without a reason. Use 403 with a reason for refusals (the token of the row is missing or expired, the row may not be changed).</para>
    /// <para>Say what the refusal means, never let the client guess it from the status (the "X-Sw-Outcome" header, see <see cref="SysWeaver.Net.HttpOutcomes"/>):
    /// <c>throw HttpResponseException.AlreadyDone("Process #12 has already ended")</c> when the goal is already met (the client shows "not needed"),
    /// <c>HttpResponseException.Gone(...)</c> when the item of the row no longer exists, and a plain 409 / 403 (or <c>HttpResponseException.Refused(...)</c>)
    /// for everything else (the client shows "not done" with the reason).</para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class TableDataActionsAttribute : TableDataRawFormatAttribute
    {

        static object[] Parse(String text, String title, String url, String icon, params String[] moreButtons)
        {
            List<object> buttons = new List<object>();
            buttons.Add(String.Join('|', text ?? "", title ?? "", url ?? "", icon ?? ""));
            if (moreButtons != null)
            {
                var bl = moreButtons.Length;
#if DEBUG
                if ((bl & 3) != 0)
                    throw new ArgumentException("The more buttons must contains a multiple of 4 string, one for text, title, url and icon", nameof(moreButtons));
#endif//DEBUG
                for (int i = 0; i < bl; i += 4)
                    buttons.Add(String.Join('|', moreButtons[i] ?? "", moreButtons[i + 1] ?? "", moreButtons[i + 2] ?? "", moreButtons[i + 3] ?? ""));
            }
            return buttons.ToArray();
        }


        static object[] Parse(String[] moreButtons)
        {
            List<object> buttons = new List<object>();
            if (moreButtons != null)
            {
                var bl = moreButtons.Length;
#if DEBUG
                if ((bl & 3) != 0)
                    throw new ArgumentException("The buttons must contains a multiple of 4 string, one for text, title, url and icon", nameof(moreButtons));
#endif//DEBUG
                for (int i = 0; i < bl; i += 4)
                    buttons.Add(String.Join('|', moreButtons[i] ?? "", moreButtons[i + 1] ?? "", moreButtons[i + 2] ?? "", moreButtons[i + 3] ?? ""));
            }
            return buttons.ToArray();
        }

        /// <summary>
        /// Action buttons.
        /// </summary>
        /// <param name="text">Text of the button</param>
        /// <param name="title">Title (tool tip) of the button</param>
        /// <param name="url">Url to the get request that will be performed on click.\nIf it start's with a '@' the url will be opened in a new tab.\nIf it start's with a '&amp;' the url will be opened in the same tab.</param>
        /// <param name="icon">Icon class name or url</param>
        /// <param name="moreButtons">An optional array of extra buttons, 4 strings per button following a: "text", "title", "url" and "icon" pattern</param>
        /// <exception cref="ArgumentException">The number of strings in <paramref name="moreButtons"/> isn't a multiple of 4 (debug builds only; release builds throw an <see cref="IndexOutOfRangeException"/>)</exception>
        public TableDataActionsAttribute(String text, String title, String url, String icon, params String[] moreButtons)
            :
            base(TableDataFormats.Actions, Parse(text, title, url, icon, moreButtons))
        {
        }

        /// <summary>
        /// Action buttons.
        /// </summary>
        /// <param name="buttons">An array of buttons, 4 strings per button following a: "text", "title", "url" and "icon" pattern</param>
        /// <exception cref="ArgumentException">The number of strings in <paramref name="buttons"/> isn't a multiple of 4 (debug builds only; release builds throw an <see cref="IndexOutOfRangeException"/>)</exception>
        public TableDataActionsAttribute(params String[] buttons)
            :
            base(TableDataFormats.Actions, Parse(buttons))
        {
        }

    }


}
