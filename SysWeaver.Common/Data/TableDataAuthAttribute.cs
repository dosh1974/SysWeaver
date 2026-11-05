using System;

namespace SysWeaver.Data
{
    /// <summary>
    /// Format values as an auth requirement (a <see cref="TableDataFormats.Tags"/> column with the "Auth" option):
    /// null = open to anyone (shown as an accent "Open" tag, so open end points stand out),
    /// "" = any signed in user (shown as an "Any signed in user" tag),
    /// else a comma separated list of tokens where at least one is required (one tag per token).
    /// Clicking a tag copies it, clicking the cell copies the raw value.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class TableDataAuthAttribute : TableDataRawFormatAttribute
    {
        /// <summary>
        /// Format values as an auth requirement.
        /// </summary>
        public TableDataAuthAttribute() : base(TableDataFormats.Tags, "{^0}", "{2}", "{0}", true, "Auth")
        {
        }
    }


}
