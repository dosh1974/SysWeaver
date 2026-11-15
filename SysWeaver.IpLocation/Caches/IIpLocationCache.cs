using System;
using System.Threading.Tasks;

namespace SysWeaver.IpLocation.Caches
{
    /// <summary>
    /// A cache of ip locations
    /// </summary>
    public interface IIpLocationCache
    {
        /// <summary>
        /// Get the location of an ip address from the cache, if it isn't cached it is fetched using the supplied function (and cached)
        /// </summary>
        /// <param name="ip">The ip address</param>
        /// <param name="getFromSource">Gets the location from the sources (if the cache doesn't have it)</param>
        /// <returns>The location, null if it is unknown</returns>
        ValueTask<IpLocation> Get(String ip, Func<String, Task<IpLocation>> getFromSource);
    }
}
