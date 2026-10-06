using System;

namespace SysWeaver
{
    /// <summary>
    /// Request sent to <see cref="Security.ILanCertificateManager.GetCert(GetLanCertRequest)"/>.
    /// </summary>
    public sealed class GetLanCertRequest
    {
        /// <summary>
        /// The name of the domain to get the cert for (matched case insensitive against the domains configured in the manager).
        /// </summary>
        public String DomainName;
        /// <summary>
        /// The password that the manager uses to protect the returned pfx data.
        /// Sent to the server, so the connection to the manager should use https.
        /// </summary>
        public String Password;
        /// <summary>
        /// The change counter of the previously retrieved certificate (or 0 for the first call)
        /// </summary>
        public long Cc;
    }
}
