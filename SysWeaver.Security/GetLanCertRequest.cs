using System;

namespace SysWeaver
{
    public sealed class GetLanCertRequest
    {
        /// <summary>
        /// The name of the domain to get the cert for
        /// </summary>
        public String DomainName;
        /// <summary>
        /// A password that will be used for the pfx
        /// </summary>
        public String Password;
        /// <summary>
        /// The change counter of the previously retrieved certificate (or 0 for the first call)
        /// </summary>
        public long Cc;
    }
}
