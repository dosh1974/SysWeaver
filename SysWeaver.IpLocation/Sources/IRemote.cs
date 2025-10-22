using System;
using System.Threading.Tasks;

namespace SysWeaver.IpLocation.Sources
{
    public interface IRemote : IDisposable
    {
        Task<IpLocation> GetLocation(string ip);

    }

}
