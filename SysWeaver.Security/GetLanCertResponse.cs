using System;

namespace SysWeaver
{
    public sealed class GetLanCertResponse
    {
        /// <summary>
        /// The bytes of the pfx certificate
        /// </summary>
        public Byte[] CertPfx;
        
        /// <summary>
        /// The current change counter, supply this int the next GetCert call in the GetCertRequest.Cc parameter
        /// </summary>
        public long Cc;
    }
}
