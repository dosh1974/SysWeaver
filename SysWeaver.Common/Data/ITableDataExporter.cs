using System;
using System.Threading.Tasks;

namespace SysWeaver.Data
{
    /// <summary>
    /// Interface that can be used for exporting data
    /// </summary>
    public interface ITableDataExporter
    {
        /// <summary>
        /// Menu name
        /// </summary>
        String Name { get; }

        /// <summary>
        /// Menu description
        /// </summary>
        String Desc { get; }

        /// <summary>
        /// Menu icon
        /// </summary>
        String Icon { get; }

        /// <summary>
        /// Used to sort data exportes
        /// </summary>
        double Order { get; }


        /// <summary>
        /// Require the user to be logged in.
        /// </summary>
        bool RequireUser { get; }


        /// <summary>
        /// Export a data table to a file
        /// </summary>
        /// <param name="tableData">The data to export</param>
        /// <param name="context">The HttpServerRequest context (wrapped in an object for exporters that don't need the dependency)</param>
        /// <param name="options">Export options</param>
        /// <returns>A file or linq in memory</returns>
        Task<MemoryFile> Export(BaseTableData tableData, Object context = null, TabelDataExportOptions options = null);
    }




}
