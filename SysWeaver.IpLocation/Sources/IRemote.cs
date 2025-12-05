using System;
using System.Threading.Tasks;
using SysWeaver.Remote;

namespace SysWeaver.IpLocation.Sources
{
    public interface IRemote : IDisposable
    {
        /// <summary>
        /// Get the estimated geolocation location of an IP
        /// </summary>
        /// <param name="ip"></param>
        /// <returns></returns>
        [RemoteEndPoint("Api/IpLocation/" + nameof(GetLocation), HttpEndPointTypes.Post)]
        Task<IpLocation> GetLocation(string ip);

    }

}
