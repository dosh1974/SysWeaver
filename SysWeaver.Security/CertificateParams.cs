using System;

namespace SysWeaver.Security
{

    /// <summary>
    /// Parameters for certificates that is signed by us with another certificate (including self signed)
    /// </summary>
    public class CertificateParams : CertificateBaseParams
    {
        /// <summary>
        /// The name of this certificate (CN in certificate), can use EnvInfo variables:
        ///             $(AppName) = Application name.
        ///             $(AppStart) = Application start time as "yyyy-MM-hh hh:mm:ss".
        ///             $(Is64BitProcess) = "True" if the process is running as a 64-bit process, else "False"
        ///             $(OSVersion) = The version of the OS
        ///             $(Platform) = The platform, ex "WinNT", "Unix".
        /// Null or empty will use the default: SysWeaver.App.$(CommonName)
        /// </summary>
        public String CommonName;

        /// <summary>
        /// Names to certify (typically list of DNS names), ex: "www.sysweaver.com".
        /// Subject alternative names (SAN's) in the certificate.
        /// </summary>
        public String[] Names;

        /// <summary>
        /// Number of days that the generated certificate should be valid.
        /// Minimum is 5 days.
        /// </summary>
        public int ValidDays = 3660;

        /// <summary>
        /// True to use default subject names, such as:
        /// localhost
        /// 127.0.0.1
        /// [ComputerName]
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
        /// Number of RSA bit's to use
        /// </summary>
        public int RsaBits = 2048;

        /// <summary>
        /// If true, LAN ip's are included, ex: "192.168.1.2"
        /// </summary>
        public bool IncludeLanIPs = true;

        /// <summary>
        /// If true local host is included (localhost dns and IPV4 and 6 ip's)
        /// </summary>
        public bool IncludeLocalHost = true;

        /// <summary>
        /// If true the host name (machine name) is included.
        /// </summary>
        public bool IncludeMachineName = true;


        

    }



}
