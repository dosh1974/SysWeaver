using System;

namespace SysWeaver.Data
{
    public sealed class TabelDataExportOptions
    {
        /// <summary>
        /// Suggested filename (no extension or path)
        /// </summary>
        public String Filename = "Table";

        /// <summary>
        /// Don't output any headers
        /// </summary>
        public bool NoHeaders;

        /// <summary>
        /// True to output in portrait mode
        /// </summary>
        public bool Portrait;
    }


}
