namespace SysWeaver.Security
{
    public class CertificateProviderParams : CertificateParams
    {
        /// <summary>
        /// The minimum valid hours for a cached certificate, the cached certificate is renewed if there is less than this many hours left before expiration.
        /// </summary>
        public int MinValidHours = 3 * 24;

        /// <summary>
        /// Notify that a new certificate will have to be created this many hours before expiration
        /// </summary>
        public int RenewBeforeExpirationHours = 48;
    }


}
