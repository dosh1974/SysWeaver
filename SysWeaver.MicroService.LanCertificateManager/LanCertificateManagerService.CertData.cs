using System;
using System.Security.Cryptography.X509Certificates;

namespace SysWeaver.MicroService
{
    public sealed partial class LanCertificateManagerService
    {
        sealed class CertData
        {
            public readonly X509Certificate2 Cert;
            public readonly long Cc;

            public CertData(X509Certificate2 cert)
            {
                Cert = cert;
                Cc = (DateTime.UtcNow - new DateTime(2026, 1, 1)).Ticks;
            }
        }



    }
}
