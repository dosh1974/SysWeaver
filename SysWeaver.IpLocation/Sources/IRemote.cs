using System;
using System.Threading.Tasks;
using SysWeaver.Remote;

namespace SysWeaver.IpLocation.Sources
{
    /// <summary>
    /// The remote api of another ip location service
    /// </summary>
    public interface IRemote : IDisposable
    {
        /// <summary>
        /// Get the estimated geolocation location of an IP
        /// </summary>
        /// <param name="ip">The ip address</param>
        /// <returns>The location, null if it isn't known</returns>
        [RemoteEndPoint("Api/IpLocation/" + nameof(GetLocation), HttpEndPointTypes.Post)]
        Task<IpLocation> GetLocation(string ip);

    }

}
