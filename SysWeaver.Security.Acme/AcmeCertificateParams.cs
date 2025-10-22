using System;

namespace SysWeaver.Security
{
    public sealed class AcmeCertificateParams : CertificateBaseParams
    {
        public AcmeCertificateParams()
        {
            Filename = @"$(CommonApplicationData)\SysWeaver_AcmeCertificates\$(AuthApi)_$(Email)_$(DomainName)_$(Hash).pfx";
        }

        /// <summary>
        /// The base url for the ACME service (directory)
        /// </summary>
//        public String AuthApi = "https://acme-staging-v02.api.letsencrypt.org/directory";
        //public String AuthApi = "https://acme-v02.api.letsencrypt.org/directory";
        //public String AuthApi = "https://api.buypass.com/acme/directory"";
        public String AuthApi = "https://acme-v02.api.letsencrypt.org/directory";

        /// <summary>
        /// Filename for the account key, variables:
        ///             $(BaseUrl) = Base url of the certificated authority API.
        ///             $(Email) = Email.
        ///             $(DomainName) = Domain name.
        ///             $(Hash) = Hash of the above
        /// Can use path variables, ex:
        ///             $(CommonApplicationData) = The directory that serves as a common repository for application-specific data that is used by all users.
        ///             $(LocalApplicationData) = The directory that serves as a common repository for application-specific data that is used by the current, non-roaming user.
        ///             $(ApplicationData) = The directory that serves as a common repository for application-specific data for the current roaming user (typically settings that should be shared between systems).
        ///             $(MyPictures) = The My Pictures folder.
        ///             $(AppName) = Application name.
        ///             $(Executable) = Full path to the executable.
        ///             $(ExecutableDir) = ExecutableDir.
        ///             $(ExecutableBase) = Full path to the folder where the application is.
        /// </summary>
        public String AccountFilename = @"$(CommonApplicationData)\SysWeaver_AcmeAccounts\$(AuthApi)_$(Email)_$(DomainName)_$(Hash).key";


        /// <summary>
        /// For certificates to be issued, files must be served using HTTP on port 80.
        /// If this is non-empty, this is a folder that is serving it's files at http://domainname/.well-known/acme-challenge/ .
        /// Otherwise this service will open a http server on port 80 (during the challenge phase only).
        /// Can use path variables, ex:
        ///             $(CommonApplicationData) = The directory that serves as a common repository for application-specific data that is used by all users.
        ///             $(LocalApplicationData) = The directory that serves as a common repository for application-specific data that is used by the current, non-roaming user.
        ///             $(ApplicationData) = The directory that serves as a common repository for application-specific data for the current roaming user (typically settings that should be shared between systems).
        ///             $(MyPictures) = The My Pictures folder.
        ///             $(AppName) = Application name.
        ///             $(Executable) = Full path to the executable.
        ///             $(ExecutableDir) = ExecutableDir.
        ///             $(ExecutableBase) = Full path to the folder where the application is.
        /// </summary>
        public String ChallengeFolder;

        /// <summary>
        /// The name of the domain to create a certificate for , can use EnvInfo variables:
        ///             $(AppName) = Application name.
        ///             $(AppStart) = Application start time as "yyyy-MM-hh hh:mm:ss".
        ///             $(Is64BitProcess) = "True" if the process is running as a 64-bit process, else "False"
        ///             $(OSVersion) = The version of the OS
        ///             $(Platform) = The platform, ex "WinNT", "Unix".
        /// </summary>
        public String DomainName;


        /// <summary>
        /// To create a full certificate, the intermediate certificates (public parts) must be included.
        /// This is an array of file names that should contain these certificates.
        /// Can use path variables, ex:
        ///             $(CommonApplicationData) = The directory that serves as a common repository for application-specific data that is used by all users.
        ///             $(LocalApplicationData) = The directory that serves as a common repository for application-specific data that is used by the current, non-roaming user.
        ///             $(ApplicationData) = The directory that serves as a common repository for application-specific data for the current roaming user (typically settings that should be shared between systems).
        ///             $(MyPictures) = The My Pictures folder.
        ///             $(AppName) = Application name.
        ///             $(Executable) = Full path to the executable.
        ///             $(ExecutableDir) = ExecutableDir.
        ///             $(ExecutableBase) = Full path to the folder where the application is.
        /// </summary>
        public String[] ImportCertFiles;

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