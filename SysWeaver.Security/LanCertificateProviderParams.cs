using System;

namespace SysWeaver.Security
{
    /// <summary>
    /// Parameters for <see cref="LanCertificateProvider"/>.
    /// The inherited certificate parameters are used for the self-signed fallback certificate, and <see cref="CertificateBaseParams.Password"/> is also used to protect the pfx sent by the manager.
    /// </summary>
    public sealed class LanCertificateProviderParams : CertificateProviderParams
    {
        /// <summary>
        /// Create parameters with the defaults, the fallback certificate is cached as "$(CommonApplicationData)\SysWeaver_AppData_$(AppName)\Lan.pfx".
        /// </summary>
        public LanCertificateProviderParams()
        {
            Filename = @"$(CommonApplicationData)/SysWeaver_AppData_$(AppName)/Lan.pfx";
        }

        /// <summary>
        /// Name of a text file that contains the base url to the server that is hosting the Lan Certificate Manager service.
        /// The first non-comment line is used. Can use path variables.
        /// </summary>
        public String ServerConfigFile = "$(KeyFolder)/LanCertificateProvider_Server.txt";

        /// <summary>
        /// Credentials to use for communicating with the Lan Certificate Manager service
        /// </summary>
        public CredentialParams ServerCreds = new CredentialParams
        {
            CredFile = "$(KeyFolder)/LanCertificateProvider_SwLanCertManager.txt",
        };

        /// <summary>
        /// The domain name to get the cert for.
        /// If this is the name of an existing file, the first non-comment line of that file is used as the domain name.
        /// </summary>
        public String DomainName = "$(KeyFolder)/LanCertificateProvider_DomainName.txt";


    }


}
