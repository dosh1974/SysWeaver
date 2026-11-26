using System.Threading.Tasks;

namespace SysWeaver.Net
{
    /// <summary>
    /// A service that finds the estimated location of ip addresses (ex: SysWeaver.IpLocation.IpLocationService).
    /// Other services can find it with ServiceManager.TryGet&lt;IIpLocation&gt;() without a reference to the implementation.
    /// </summary>
    public interface IIpLocation
    {
        /// <summary>
        /// Get the estimated location of an ip address
        /// </summary>
        /// <param name="ip">The ip address</param>
        /// <returns>The location, null if it isn't known</returns>
        Task<Location> GetLocation(string ip);
    }

}
