namespace SysWeaver.Security
{
    /// <summary>
    /// Parameters for certificate providers that generate (and cache) certificates locally, adds renewal settings to <see cref="CertificateParams"/>.
    /// </summary>
    public class CertificateProviderParams : CertificateParams
    {
        /// <summary>
        /// The minimum valid hours for a cached certificate, a cached certificate is replaced with a new one if there is less than this many hours left before expiration.
        /// Values below 2 are clamped to 2.
        /// </summary>
        public int MinValidHours = 3 * 24;

        /// <summary>
        /// The number of hours before expiration when the cached certificate is dropped and <see cref="ICertificateProvider.OnChanged"/> is raised (so that consumers fetch a renewed certificate).
        /// The effective value is at least half of <see cref="MinValidHours"/>.
        /// Not used by <see cref="LanCertificateProvider"/> (that provider polls on a fixed interval instead).
        /// </summary>
        public int RenewBeforeExpirationHours = 48;
    }


}
