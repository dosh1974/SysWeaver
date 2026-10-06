using System.Collections.Generic;

namespace SysWeaver.Data
{
    /// <summary>
    /// Service objects implementing this interface can expose table exporters
    /// </summary>
    /// <remarks>
    /// The explore service (SysWeaver.MicroService.HttpServer) registers the exporters when the service instance is added to the service manager and unregisters them when it's removed.
    /// </remarks>
    public interface IHaveTableDataExporters
    {
        /// <summary>
        /// Table exporters (can be used to export tables), null entries are ignored.
        /// </summary>
        /// <remarks>
        /// Read when the service is added and again when it's removed, so it should return the same exporters each time.
        /// </remarks>
        IReadOnlyList<ITableDataExporter> TableDataExporters { get; }

    }


}
