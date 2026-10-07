using System;

namespace SysWeaver.Security
{
    /// <summary>
    /// Parameters for <see cref="SignedCertificateProvider"/>.
    /// </summary>
    public sealed class SignedCertificateProviderParams : CertificateProviderParams
    {
        /// <summary>
        /// Create parameters with the defaults, the certificate is cached as "$(CommonApplicationData)\SysWeaver_AppData_$(AppName)\Signed.pfx".
        /// </summary>
        public SignedCertificateProviderParams()
        {
            Filename = @"$(CommonApplicationData)/SysWeaver_AppData_$(AppName)/Signed.pfx";
        }

        /// <summary>
        /// Name of the root (CA) certificate file (.pfx) used to sign the generated certificate.
        /// Must contain the private keys. The file is monitored for changes.
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
        public string RootFilename;

        /// <summary>
        /// If the root certificate's private keys are protected, enter the password here.
        /// Optionally this can be a filename containing the password (encoded as an UTF-8 text file).
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
        public string RootPassword;


        /// <summary>
        /// If root publishing is enabled, this is the template for the url (relative to the web root) where the public root certificate is published.
        /// In addition to the EnvInfo variables below, $(Filename) is the root certificate file name without extension and $(Ext) is "pem" and "crt" (both are published).
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

        public String RootCertUri = "certificates/$(Filename).$(Ext)";


        /// <summary>
        /// If true, the public part of the root certificate is exposed at the <see cref="RootCertUri"/> and thus can be downloaded (and maybe added to accepted roots).
        /// Requires the provider to also be registered as an HTTP server module.
        /// </summary>
        public bool PublishRoot = true;

    }


}
