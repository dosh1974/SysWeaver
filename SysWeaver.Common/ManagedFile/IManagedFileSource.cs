using System;
using System.Threading.Tasks;

namespace SysWeaver
{
    /// <summary>
    /// A source of file data for a <see cref="ManagedFile"/> (ex: a local file or a http url), it monitors the file and reports changes to the manager.
    /// Custom sources can be registered for a schema using <see cref="ManagedFile.TryAddSchema"/>.
    /// Disposing it stops the monitoring.
    /// </summary>
    public interface IManagedFileSource : IDisposable
    {
        /// <summary>
        /// Read the file data now (should not throw, failures are reported using <see cref="ManagedFileData.Ex"/>)
        /// </summary>
        /// <returns>The current data, a data object with an exception on failure, or null if the data is known to be unchanged</returns>
        Task<ManagedFileData> TryGetNow();
    }

}
