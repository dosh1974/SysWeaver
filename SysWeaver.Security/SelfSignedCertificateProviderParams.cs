namespace SysWeaver.Security
{
    public sealed class SelfSignedCertificateProviderParams : CertificateProviderParams
    {
        public SelfSignedCertificateProviderParams()
        {
            Filename = @"$(CommonApplicationData)\SysWeaver_AppData_$(AppName)\SelfSigned.pfx";
        }


    }


}
