using System;
using System.Threading.Tasks;

namespace SysWeaver.IpLocation.Sources
{
    public interface IIpLocationSource
    {
        String Name { get; }
        Task<IpLocation> LookUp(String ip);
    }

}
