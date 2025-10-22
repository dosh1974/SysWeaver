namespace SysWeaver.Security
{
    public sealed class FileCertificateProviderParams : ManagedFileParams
    {
        /// <summary>
        /// Alias for the Location field.
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
        public string Filename
        {
            get => Location;
            set => Location = value;
        }

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
        public string CertPassword;
    }



}
