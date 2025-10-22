using SysWeaver.Data;
using System;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Use to export some table data API to some format
    /// </summary>
    public sealed class ExportTableApiRequest
    {
        /// <summary>
        /// The API to export
        /// </summary>
        public String Api;

        /// <summary>
        /// Request paramaters (filtering etc)
        /// </summary>
        public TableDataRequest Req;

        /// <summary>
        /// The name of the exporter to use
        /// </summary>
        public String ExportAs;

        /// <summary>
        /// Options
        /// </summary>
        public TabelDataExportOptions Options;

    }




}
