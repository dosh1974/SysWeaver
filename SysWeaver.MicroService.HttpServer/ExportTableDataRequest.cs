using SysWeaver.Data;
using System;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Use to export some table data to some format
    /// </summary>
    public sealed class ExportTableDataRequest
    {
        /// <summary>
        /// The data to export
        /// </summary>
        public TableData Data;

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
