using System;
using System.Collections.Generic;

namespace SysWeaver.Data
{
    /// <summary>
    /// Action buttons.
    /// </summary>
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
                if ((bl & 3) != 0)
                    throw new ArgumentException("The more buttons must contains a multiple of 4 string, one for text, title, url and icon", nameof(moreButtons));
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
                if ((bl & 3) != 0)
                    throw new ArgumentException("The buttons must contains a multiple of 4 string, one for text, title, url and icon", nameof(moreButtons));
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
        public TableDataActionsAttribute(String text, String title, String url, String icon, params String[] moreButtons)
            :
            base(TableDataFormats.Actions, Parse(text, title, url, icon, moreButtons))
        {
        }

        /// <summary>
        /// Action buttons.
        /// </summary>
        /// <param name="buttons">An array of buttons, 4 strings per button following a: "text", "title", "url" and "icon" pattern</param>
        public TableDataActionsAttribute(params String[] buttons)
            :
            base(TableDataFormats.Actions, Parse(buttons))
        {
        }

    }


}
