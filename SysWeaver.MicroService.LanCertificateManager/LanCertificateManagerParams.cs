using System;
using SysWeaver.Security;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Parameters for the <see cref="LanCertificateManagerService"/>.
    /// The inherited values are copied to the <see cref="AcmeCertificateParams"/> of every domain: <see cref="CertificateBaseParams.Email"/> (the ACME account contact) is required,
    /// and the <see cref="CertificateBaseParams.Filename"/> template can use $(AuthApi), $(Email), $(DomainName) and $(Hash) like <see cref="AcmeCertificateParams"/>.
    /// </summary>
    public sealed class LanCertificateManagerParams : CertificateBaseParams
    {
        /// <summary>
        /// Sets the default certificate file name template (one file per auth api, e-mail, domain and hash)
        /// </summary>
        public LanCertificateManagerParams()
        {
            Filename = @"$(CommonApplicationData)/SysWeaver_AppData_$(AppName)/ManagedCerts/$(AuthApi)_$(Email)_$(DomainName)_$(Hash).pfx";

        }

        /// <summary>
        /// The ACME directory url of the certificate authority, default is the Let's Encrypt production directory
        /// </summary>
        public String AuthApi = "https://acme-v02.api.letsencrypt.org/directory";


        /// <summary>
        /// The domain names to manage certificates for (required, no default)
        /// </summary>
        public String[] Domains;

    }
}
