namespace SysWeaver.Security
{
    /// <summary>
    /// Parameters for <see cref="SelfSignedCertificateProvider"/>.
    /// </summary>
    public sealed class SelfSignedCertificateProviderParams : CertificateProviderParams
    {
        /// <summary>
        /// Create parameters with the defaults, the certificate is cached as "$(CommonApplicationData)\SysWeaver_AppData_$(AppName)\SelfSigned.pfx".
        /// </summary>
        public SelfSignedCertificateProviderParams()
        {
            Filename = @"$(CommonApplicationData)/SysWeaver_AppData_$(AppName)/SelfSigned.pfx";
        }


    }


}
