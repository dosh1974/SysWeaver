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
        /// Request parameters (filtering etc)
        /// </summary>
        public TableDataRequest Req;

        /// <summary>
        /// The name of the exporter to use
        /// </summary>
        public String ExportAs;

        /// <summary>
        /// Options
        /// </summary>
        public TableDataExportOptions Options;

        /// <summary>
        /// Only used when the export is started as a background job (StartExportTableApi / StartExportTableData): only one running job per key and user
        /// (the running one is returned), ex: "sw.table.export|&lt;url&gt;|&lt;mode&gt;|&lt;exporter&gt;". Optional.
        /// </summary>
        public String Key;

        /// <summary>
        /// Only used when the export is started as a background job: the title of the job (shown in the activity list), null for "Exporting &lt;file name&gt;".
        /// </summary>
        public String Title;

    }




}
