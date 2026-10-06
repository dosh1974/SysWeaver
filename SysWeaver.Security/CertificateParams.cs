using System;

namespace SysWeaver.Security
{

    /// <summary>
    /// Parameters for certificates that are generated locally, either self-signed or signed by a local CA certificate.
    /// Consumed by <see cref="SignedCertificateCreator"/>.
    /// </summary>
    public class CertificateParams : CertificateBaseParams
    {
        /// <summary>
        /// The name of this certificate (CN in certificate), can use EnvInfo variables:
        ///             $(AppName) = Application name.
        ///             $(AppStart) = Application start time (UTC) as "yyyy-MM-dd HH:mm:ss".
        ///             $(Is64BitProcess) = "True" if the process is running as a 64-bit process, else "False"
        ///             $(OSVersion) = The version of the OS
        ///             $(Platform) = The platform, ex "WinNT", "Unix".
        /// Null or empty will use the default: "SysWeaver.App.$(AppName)".
        /// </summary>
        public String CommonName;

        /// <summary>
        /// Additional DNS names to certify, ex: "www.sysweaver.com".
        /// Added as DNS subject alternative names (SAN's) in the certificate (sorted, duplicates ignored case insensitive).
        /// </summary>
        public String[] Names;

        /// <summary>
        /// Number of days that the generated certificate should be valid (from now).
        /// Values below 5 are clamped to 5. For CA-signed certificates the validity is also limited to the validity of the CA certificate.
        /// </summary>
        public int ValidDays = 3660;

        /// <summary>
        /// Not used by the certificate generation code, use <see cref="IncludeLocalHost"/>, <see cref="IncludeMachineName"/> and <see cref="IncludeLanIPs"/> instead.
        /// </summary>
        public bool UseDefaultNames = true;

        /// <summary>
        /// Certificate distinguished name qualifier (dnQualifier in certificate), can use EnvInfo variables.
        /// </summary>
        public String DistinguishedNameQualifier;
        /// <summary>
        /// Certificate serial number (serialNumber in certificate), can use EnvInfo variables.
        /// </summary>
        public String SerialNumber;
        /// <summary>
        /// Certificate title (title in certificate), can use EnvInfo variables.
        /// </summary>
        public String Title;
        /// <summary>
        /// Certificate surname (SN in certificate), can use EnvInfo variables.
        /// </summary>
        public String SurName;
        /// <summary>
        /// Certificate given name (GN in certificate), can use EnvInfo variables.
        /// </summary>
        public String GivenName;
        /// <summary>
        /// Certificate initials (initials in certificate), can use EnvInfo variables.
        /// </summary>
        public String Initials;
        /// <summary>
        /// Certificate pseudonym (pseudonym in certificate), can use EnvInfo variables.
        /// </summary>
        public String Pseudonym;
        /// <summary>
        /// Certificate generation qualifier (generationQualifier in certificate), can use EnvInfo variables.
        /// </summary>
        public String GenerationQualifier;



        /// <summary>
        /// Number of RSA key bits to use.
        /// Values less than or equal to zero use 2048, other values are rounded up to the nearest power of two.
        /// </summary>
        public int RsaBits = 2048;

        /// <summary>
        /// If true, the IP addresses of the local network interfaces are added as SAN's, ex: "192.168.1.2".
        /// Note that a certificate cached on disc is regenerated if the set of local IP addresses changes.
        /// </summary>
        public bool IncludeLanIPs = true;

        /// <summary>
        /// If true, "localhost" and the IPv4 and IPv6 loopback addresses are added as SAN's.
        /// </summary>
        public bool IncludeLocalHost = true;

        /// <summary>
        /// If true, the machine name (<see cref="System.Environment.MachineName"/>) is added as a DNS SAN.
        /// </summary>
        public bool IncludeMachineName = true;


        

    }



}
