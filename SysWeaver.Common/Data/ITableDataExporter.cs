using System;
using System.Threading.Tasks;

namespace SysWeaver.Data
{
    /// <summary>
    /// Interface that can be used for exporting table data to some file format.
    /// </summary>
    /// <remarks>
    /// Exporters are exposed by services implementing <see cref="IHaveTableDataExporters"/> and are registered by the explore service (SysWeaver.MicroService.HttpServer),
    /// which shows them as menu items and calls <see cref="Export"/> through its web API.
    /// </remarks>
    public interface ITableDataExporter
    {
        /// <summary>
        /// Menu name, also used as the unique (case sensitive) key of the exporter, an exporter with a name that is already registered is ignored
        /// </summary>
        String Name { get; }

        /// <summary>
        /// Menu description
        /// </summary>
        String Desc { get; }

        /// <summary>
        /// Menu icon (icon class)
        /// </summary>
        String Icon { get; }

        /// <summary>
        /// Used to sort data exporters in the menu (ascending)
        /// </summary>
        double Order { get; }


        /// <summary>
        /// Require the user to be logged in.
        /// </summary>
        /// <remarks>
        /// The menu item is hidden from anonymous users and the explore export APIs reject anonymous calls for this exporter.
        /// Other callers of <see cref="Export"/> are not checked.
        /// </remarks>
        bool RequireUser { get; }


        /// <summary>
        /// Export a data table to a file
        /// </summary>
        /// <param name="tableData">The data to export</param>
        /// <param name="context">The HttpServerRequest context (wrapped in an object for exporters that don't need the dependency)</param>
        /// <param name="options">Export options, null to use the defaults</param>
        /// <returns>A file (or link) in memory, may be null if the export couldn't be performed</returns>
        Task<MemoryFile> Export(BaseTableData tableData, Object context = null, TableDataExportOptions options = null);
    }




}
