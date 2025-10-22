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
        ///             $(AppName) = Application name.
        ///             $(Executable) = Full path to the executable.
        ///             $(ExecutableDir) = ExecutableDir.
        ///             $(ExecutableBase) = Full path to the folder where the application is.
        ///             $(Cc) = A "unique" id for this process
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
        ///             $(AppName) = Application name.
        ///             $(Executable) = Full path to the executable.
        ///             $(ExecutableDir) = ExecutableDir.
        ///             $(ExecutableBase) = Full path to the folder where the application is.
        ///             $(Cc) = A "unique" id for this process
        /// Can use EnvInfo variables:
        ///             $(AppName) = Application name.
        ///             $(AppStart) = Application start time as "yyyy-MM-hh hh:mm:ss".
        ///             $(Is64BitProcess) = "True" if the process is running as a 64-bit process, else "False"
        ///             $(OSVersion) = The version of the OS
        ///             $(Platform) = The platform, ex "WinNT", "Unix".
        /// </summary>
        public string RootPassword;


        /// <summary>
        /// If root publishing is enabled, this is the template for the file names.
        ///             $(Ext) = File extension (.pem, .crt) etc.
        ///             $(Filename) = The filename part of the RootFilename.
        /// Can use EnvInfo variables:
        ///             $(AppName) = Application name.
        ///             $(AppStart) = Application start time as "yyyy-MM-hh hh:mm:ss".
        ///             $(Is64BitProcess) = "True" if the process is running as a 64-bit process, else "False"
        ///             $(OSVersion) = The version of the OS
        ///             $(Platform) = The platform, ex "WinNT", "Unix".
        /// </summary>

        public String RootCertUri = "certificates/$(Filename).$(Ext)";


        /// <summary>
        /// If true, the public part of the root certificate is exposed at the RootCertUri and thus can be downloaded (and maybe added to accepted roots).
        /// </summary>
        public bool PublishRoot = true;

    }


}
