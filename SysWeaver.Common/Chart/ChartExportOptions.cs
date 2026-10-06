using System;

namespace SysWeaver.Chart
{
    /// <summary>
    /// Options passed to <see cref="IChartExporter.Export"/>, exporters use the defaults if null is passed.
    /// </summary>
    public sealed class ChartExportOptions
    {
        /// <summary>
        /// Suggested filename (no extension or path)
        /// </summary>
        public String Filename = "Chart";

        /// <summary>
        /// True to swap the auto detected landscape / portrait mode (for exporters producing paged output, such as pdf)
        /// </summary>
        public bool SwapLandscapePortrait;

    }


}
