using System;

namespace SysWeaver.Security
{

    /// <summary>
    /// Base certificate parameters shared by locally generated (self-signed / CA-signed) and ACME certificates:
    /// where the generated certificate is cached, the password protecting it and the subject fields.
    /// </summary>
    /// <remarks>
    /// The subject fields are validated by <see cref="SignedCertificateCreator"/>: they may not contain <c>'='</c> or <c>','</c>.
    /// </remarks>
    public class CertificateBaseParams
    {
        /// <summary>
        /// The .pfx file where the generated certificate (including private key) is cached between executions.
        /// A PEM encoded public certificate is also written next to it using the ".crt" extension.
        /// Null or empty disables caching (a new certificate is generated every time the process starts).
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
        /// The password used to protect the private key of the cached .pfx file.
        /// The default (the application name) is not a secret, it only prevents casual use of the file; protect the file using file system permissions.
        /// Can use EnvInfo variables:
        ///             $(AppName) = Application name.
        ///             $(AppStart) = Application start time (UTC) as "yyyy-MM-dd HH:mm:ss".
        ///             $(Is64BitProcess) = "True" if the process is running as a 64-bit process, else "False"
        ///             $(OSVersion) = The version of the OS
        ///             $(Platform) = The platform, ex "WinNT", "Unix".
        /// </summary>

        public String Password = "$(AppName)";

        /// <summary>
        /// Certificate country (C in certificate), can use EnvInfo variables.
        /// Any value understood by <c>IsoCountry.TryGet</c> is accepted and converted to an ISO 3166 alpha-2 code, unknown values are ignored (no C is added).
        /// Null or empty uses the region of the current culture.
        /// </summary>
        public String Country;

        /// <summary>
        /// Certificate locality / city (L in certificate), can use EnvInfo variables.
        /// </summary>
        public String Locality;

        /// <summary>
        /// Certificate organization (O in certificate), can use EnvInfo variables.
        /// </summary>
        public String Organization = "SysWeaver";

        /// <summary>
        /// Certificate organizational unit (OU in certificate), can use EnvInfo variables.
        /// </summary>
        public String Unit = "Platform";

        /// <summary>
        /// Certificate state or province (ST in certificate), can use EnvInfo variables.
        /// </summary>
        public String State;

        /// <summary>
        /// Certificate email (E in certificate), can use EnvInfo variables.
        /// </summary>
        public String Email;



        /// <summary>
        /// Copy all values declared by <see cref="CertificateBaseParams"/> from another instance (shallow copy).
        /// </summary>
        /// <param name="p">The instance to copy from, may not be null.</param>
        public void CopyFrom(CertificateBaseParams p)
        {

            Filename = p.Filename;
            Password = p.Password;
            Country = p.Country;
            Locality = p.Locality;
            Organization = p.Organization;
            Unit = p.Unit;
            State = p.State;
            Email = p.Email;
        }
    
    }

}
