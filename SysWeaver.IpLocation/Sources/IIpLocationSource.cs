using System;
using System.Threading.Tasks;

namespace SysWeaver.IpLocation.Sources
{
    /// <summary>
    /// A source of ip locations
    /// </summary>
    public interface IIpLocationSource
    {
        /// <summary>
        /// The name of the source
        /// </summary>
        String Name { get; }

        /// <summary>
        /// Look up the location of an ip address
        /// </summary>
        /// <param name="ip">The ip address</param>
        /// <returns>The location, null if it isn't known</returns>
        /// <exception cref="Exception">If the look up failed (the next source is then tried)</exception>
        Task<IpLocation> LookUp(String ip);
    }

}
