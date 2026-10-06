using System.Collections.Generic;

namespace SysWeaver.Chart
{
    /// <summary>
    /// Service objects implementing this interface can expose chart exporters
    /// </summary>
    /// <remarks>
    /// The ChartJs service registers the exporters when the service instance is added to the service manager and unregisters them when it's removed.
    /// </remarks>
    public interface IHaveChartExporters
    {
        /// <summary>
        /// Chart exporters (can be used to export charts), null entries are ignored.
        /// </summary>
        /// <remarks>
        /// Read when the service is added and again when it's removed, so it should return the same exporters each time.
        /// </remarks>
        IReadOnlyList<IChartExporter> ChartExporters { get; }

    }

}
