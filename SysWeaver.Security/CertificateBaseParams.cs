using System;

namespace SysWeaver.Security
{

    /// <summary>
    /// Base certificate params, can be used by signed and ACME certificates
    /// </summary>
    public class CertificateBaseParams
    {
        /// <summary>
        /// Where to store the template.
        /// Can use path variables, ex:
        ///             $(CommonApplicationData) = The directory that serves as a common repository for application-specific data that is used by all users.
        ///             $(LocalApplicationData) = The directory that serves as a common repository for application-specific data that is used by the current, non-roaming user.
        ///             $(ApplicationData) = The directory that serves as a common repository for application-specific data for the current roaming user (typically settings that should be shared between systems).
        ///             $(MyPictures) = The My Pictures folder.
        ///             $(AppName) = Application name.
        ///             $(Executable) = Full path to the executable.
        ///             $(ExecutableDir) = ExecutableDir.
        ///             $(ExecutableBase) = Full path to the folder where the application is.
        ///             $(Cc) = A "unique" id for this process
        /// </summary>
        public String Filename = @"$(CommonApplicationData)\SysWeaver_AppData_$(AppName)\Cert.pfx";

        /// <summary>
        /// The password to use for the generated cert.
        /// Can use EnvInfo variables:
        ///             $(AppName) = Application name.
        ///             $(AppStart) = Application start time as "yyyy-MM-hh hh:mm:ss".
        ///             $(Is64BitProcess) = "True" if the process is running as a 64-bit process, else "False"
        ///             $(OSVersion) = The version of the OS
        ///             $(Platform) = The platform, ex "WinNT", "Unix".
        /// </summary>

        public String Password = "$(AppName)";

        /// <summary>
        /// Certificate country as an ISO 3166 Alpha 2 country code (C in certificate), can use EnvInfo variables:
        /// </summary>
        public String Country;

        /// <summary>
        /// Certificate location (L in certificate), can use EnvInfo variables:
        /// </summary>
        public String Locality;

        /// <summary>
        /// Certificate organization (O in certificate), can use EnvInfo variables:
        /// </summary>
        public String Organization = "SysWeaver";

        /// <summary>
        /// Certificate organizational unit (OU in certificate), can use EnvInfo variables:
        /// </summary>
        public String Unit = "Platform";

        /// <summary>
        /// Certificate state or province (S in certificate), can use EnvInfo variables:
        /// </summary>
        public String State;

        /// <summary>
        /// Certificate email (E), can use EnvInfo variables.
        /// </summary>
        public String Email;
    }



}
