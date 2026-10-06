using System.Threading.Tasks;
using SysWeaver.Data;

namespace SysWeaver.Chart
{
    /// <summary>
    /// Interface that can be used for exporting charts to some file format.
    /// </summary>
    /// <remarks>
    /// Exporters are exposed by services implementing <see cref="IHaveChartExporters"/> and are registered by the ChartJs service,
    /// which shows them as menu items and calls <see cref="Export"/> through its web API.
    /// </remarks>
    public interface IChartExporter
    {
        /// <summary>
        /// Menu name, also used as the unique (case sensitive) key of the exporter, an exporter with a name that is already registered is ignored
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Menu description
        /// </summary>
        string Desc { get; }

        /// <summary>
        /// Menu icon (icon class)
        /// </summary>
        string Icon { get; }

        /// <summary>
        /// Used to sort chart exporters in the menu (ascending)
        /// </summary>
        double Order { get; }

        /// <summary>
        /// Require the user to be logged in.
        /// </summary>
        /// <remarks>
        /// The ChartJs service hides the menu item from anonymous users and rejects anonymous calls to the export API for this exporter.
        /// Other callers of <see cref="Export"/> are not checked.
        /// </remarks>
        bool RequireUser { get; }

        /// <summary>
        /// The type of data that the <see cref="Export"/> method expects
        /// </summary>
        ChartExportInputTypes InputType { get; }

        /// <summary>
        /// Export a chart to a file
        /// </summary>
        /// <param name="data">The chart data, the type depends on the <see cref="InputType"/></param>
        /// <param name="context">The HttpServerRequest of the call (typed as object to avoid a dependency)</param>
        /// <param name="options">Export options, null to use the defaults</param>
        /// <returns>The exported file (may be null if the export couldn't be performed)</returns>
        Task<MemoryFile> Export(object data, object context, ChartExportOptions options = null);



    }

}
