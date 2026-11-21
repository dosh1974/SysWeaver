using System;
using System.Security.Cryptography.X509Certificates;
using SysWeaver.Security;

namespace SysWeaver.MicroService
{
    public sealed partial class LanCertificateManagerService
    {
        sealed class CertData
        {
            public readonly X509Certificate2 Cert;
            public readonly long Cc;

            /// <summary>
            /// Keep an own copy of the certificate: the provider disposes its certificate when the renewal is due, while this one is
            /// still served (exported) until the renewed certificate replaces it.
            /// </summary>
            /// <param name="cert">The certificate of the provider (not kept).</param>
            public CertData(X509Certificate2 cert)
            {
                Cert = X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx), null, CertificateTools.InMemoryKeyStorageFlags);
                Cc = (DateTime.UtcNow - new DateTime(2026, 1, 1)).Ticks;
            }
        }



    }
}
