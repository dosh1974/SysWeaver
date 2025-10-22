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
        public TabelDataExportOptions Options;

    }




}
