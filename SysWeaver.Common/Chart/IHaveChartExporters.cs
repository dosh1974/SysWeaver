using System.Collections.Generic;

namespace SysWeaver.Chart
{
    /// <summary>
    /// Service objects implementing this interface can expose chart exportes
    /// </summary>
    public interface IHaveChartExporters
    {
        /// <summary>
        /// Chart exports (can be use to export charts)
        /// </summary>
        IReadOnlyList<IChartExporter> ChartExporters { get; }

    }

}
