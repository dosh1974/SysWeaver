using System;
using System.Threading.Tasks;

namespace SysWeaver.IpLocation.Caches
{
    public interface IIpLocationCache
    {
        Task<IpLocation> Get(String ip, Func<String, Task<IpLocation>> getFromSource);
    }
}
