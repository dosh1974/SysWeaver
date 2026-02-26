
using System.Threading.Tasks;
using System;
using SysWeaver.Remote;

namespace SysWeaver.Security
{
    [RemotePathPrefix("Api/Lcm/")]
    public interface ILanCertificateManager : IDisposable
    {
        Task<GetLanCertResponse> GetCert(GetLanCertRequest r);

    }


}
