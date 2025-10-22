using System;
using SysWeaver.Chart;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Use to export some table data to some format
    /// </summary>
    public sealed class ExportChartRequest
    {
        /// <summary>
        /// The data to export (may be null for image exports)
        /// </summary>
        public ChartJsConfig Data;

        /// <summary>
        /// Data uri for images (may be null for data export)
        /// </summary>
        public String DataStr;

        /// <summary>
        /// The name of the exporter to use
        /// </summary>
        public String ExportAs;

        /// <summary>
        /// Options
        /// </summary>
        public ChartExportOptions Options;

    }

}
