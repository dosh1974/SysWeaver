using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using System;

namespace SysWeaver.Security
{

    /// <summary>
    /// Represents an object that provides a certificate
    /// </summary>
    public interface ICertificateProvider
    {
        /// <summary>
        /// Get a certificate.
        /// </summary>
        /// <returns></returns>
        Task<X509Certificate2> GetCert();

        /// <summary>
        /// An event that is fired whenever the certificate have changed.
        /// An application should restart (or re-init) to get the updated cert (calling GetCert again will return an updated cert).
        /// </summary>
        event Action OnChanged;
    }




}
