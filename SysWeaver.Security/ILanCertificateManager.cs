
using System.Threading.Tasks;
using System;
using SysWeaver.Remote;

namespace SysWeaver.Security
{
    /// <summary>
    /// Remote API of the LAN certificate manager service (implemented by the LanCertificateManager micro service),
    /// used by <see cref="LanCertificateProvider"/> to fetch certificates issued for a LAN domain.
    /// Exposed below "Api/Lcm/".
    /// </summary>
    [RemotePathPrefix("Api/Lcm/")]
    public interface ILanCertificateManager : IDisposable
    {
        /// <summary>
        /// Get the current certificate for a domain.
        /// </summary>
        /// <param name="r">The request, may not be null.</param>
        /// <returns>The certificate, or null if the certificate is unchanged since <see cref="GetLanCertRequest.Cc"/>.</returns>
        /// <exception cref="Exception">The domain is unknown to the manager.</exception>
        Task<GetLanCertResponse> GetCert(GetLanCertRequest r);

    }


}
