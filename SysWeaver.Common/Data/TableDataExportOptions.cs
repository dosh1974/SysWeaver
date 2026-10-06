using System;

namespace SysWeaver.Data
{
    /// <summary>
    /// Options passed to <see cref="ITableDataExporter.Export"/>, exporters use the defaults if null is passed.
    /// Not all exporters support all options.
    /// </summary>
    public sealed class TableDataExportOptions
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
        /// True to output in portrait mode (for exporters producing paged output, such as Excel)
        /// </summary>
        public bool Portrait;

        /// <summary>
        /// Custom string (can mean different thing for different exporters)
        /// </summary>
        public String Custom;
    }


}
