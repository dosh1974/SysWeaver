using System.Collections.Generic;

namespace SysWeaver.Data
{
    /// <summary>
    /// Service objects implementing this interface can expose table exportes
    /// </summary>
    public interface IHaveTableDataExporters
    {
        /// <summary>
        /// Table exports (can be use to export tables)
        /// </summary>
        IReadOnlyList<ITableDataExporter> TableDataExporters { get; }

    }


}
