using System;

namespace SysWeaver.Security
{
    public sealed class LanCertificateProviderParams : CertificateProviderParams
    {
        public LanCertificateProviderParams()
        {
            Filename = @"$(CommonApplicationData)\SysWeaver_AppData_$(AppName)\Lan.pfx";
        }

        /// <summary>
        /// Name of a text file that contains the base url to the server that is hosting the Lan Certificate Manager service
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
        /// The domain name to get the cert for (can be a filename)
        /// </summary>
        public String DomainName = "$(KeyFolder)/LanCertificateProvider_DomainName.txt";


    }


}
