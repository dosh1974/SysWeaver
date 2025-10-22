using System.Threading.Tasks;
using SysWeaver.Data;

namespace SysWeaver.Chart
{
    /// <summary>
    /// Interface that can be used for exporting data
    /// </summary>
    public interface IChartExporter
    {
        /// <summary>
        /// Menu name
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Menu description
        /// </summary>
        string Desc { get; }

        /// <summary>
        /// Menu icon
        /// </summary>
        string Icon { get; }

        /// <summary>
        /// Used to sort data exportes
        /// </summary>
        double Order { get; }

        /// <summary>
        /// Require the user to be logged in.
        /// </summary>
        bool RequireUser { get; }

        /// <summary>
        /// The type of data that the Export method expects
        /// </summary>
        ChartExportInputTypes InputType { get; }

        /// <summary>
        /// Export a chart to a file
        /// </summary>
        /// <param name="data">Depends on the InputType</param>
        /// <param name="context">A HttpServerRequest context</param>
        /// <param name="options"></param>
        /// <returns></returns>
        Task<MemoryFile> Export(object data, object context, ChartExportOptions options = null);



    }

}
