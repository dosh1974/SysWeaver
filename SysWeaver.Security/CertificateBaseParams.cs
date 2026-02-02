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
        ///             $(Executable) = Full path to the executable, ex: "C:\MyServices\MyService.exe"
        ///             $(ExeAppName) = Name of the executable, ex: "MyService" (this can be different from AppName)
        ///             $(ExecutableDir) = ExecutableDir, ex: "C:\MyServices"
        ///             $(ExecutableBase) = Full path to the executable, excluding it's extensions, ex: "C:\MyServices\MyService"
        ///             $(AppName) = Application name (defaults to exe app name, can be changed in config), ex: "MyService".
        ///             $(AppGuid) = A "unique" id for this process
        ///             $(AppDisplayName) = Friendly application name (defaults to de-camel cased exe app name, can be changed in config), ex: "My service".
        ///             $(MachineName) = Machine name, ex: "DESKTOP-324VHA".
        ///             $(KeyFolder) = The folder where keys are stored. ex: "C:\Keys".
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
