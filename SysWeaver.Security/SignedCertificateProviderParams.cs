using System;

namespace SysWeaver.Security
{
    public sealed class SignedCertificateProviderParams : CertificateProviderParams
    {
        public SignedCertificateProviderParams()
        {
            Filename = @"$(CommonApplicationData)\SysWeaver_AppData_$(AppName)\Signed.pfx";
        }

        /// <summary>
        /// Name of the certificate file (.pfx).
        /// Must contain the private keys.
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
        /// If the certificate's private keys are protected, enter the password here.
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
        /// If root publishing is enabled, this is the template for the file names.
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
        /// If true, the public part of the root certificate is exposed at the RootCertUri and thus can be downloaded (and maybe added to accepted roots).
        /// </summary>
        public bool PublishRoot = true;

    }


}
