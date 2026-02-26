using System;
using SysWeaver.Security;

namespace SysWeaver.MicroService
{
    public sealed class LanCertificateManagerParams : CertificateBaseParams
    {
        public LanCertificateManagerParams()
        {
            Filename = @"$(CommonApplicationData)\SysWeaver_AppData_$(AppName)\ManagedCerts\$(AuthApi)_$(Email)_$(DomainName)_$(Hash).pfx";

        }

        public String AuthApi = "https://acme-v02.api.letsencrypt.org/directory";


        public String[] Domains;

    }
}
