using System;

namespace SysWeaver
{
    /// <summary>
    /// Response from <see cref="Security.ILanCertificateManager.GetCert(GetLanCertRequest)"/>.
    /// The manager returns null (no response object) if the certificate is unchanged since <see cref="GetLanCertRequest.Cc"/>.
    /// </summary>
    public sealed class GetLanCertResponse
    {
        /// <summary>
        /// The bytes of the pfx certificate (including the private key), protected with <see cref="GetLanCertRequest.Password"/>.
        /// Null if the manager doesn't have a certificate for the domain (yet).
        /// </summary>
        public Byte[] CertPfx;
        
        /// <summary>
        /// The current change counter, supply this in the next call using <see cref="GetLanCertRequest.Cc"/>.
        /// </summary>
        public long Cc;
    }
}
