using System;

namespace SysWeaver
{
    /// <summary>
    /// Paramaters for a managed file, the credetial parameters are used for web based files
    /// </summary>
    public class ManagedFileParams : CredentialParams
    {
        public override string ToString() => String.Concat('"', Location, "\" [", base.ToString(), ']');

        /// <summary>
        /// The file location, can be located locally on disc or remote using http/https.
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
        public String Location;

        /// <summary>
        /// For local files, this is the delay in ms before invoking the onChange. 
        /// Some application may write a file using several operations, by ensuring that nothing has changed for a certain period, the odds are greater that the file is fully written
        /// </summary>
        public int LocalGraceTime = 2000;

        /// <summary>
        /// For web based files, poll for changes at this frequency
        /// </summary>
        public int HttpPollFrequency = 5000;

        /// <summary>
        /// If true and a file have changed (typically based on file data), the data will be hashed and compared to the existing data, if they are equal no change notification will be sent
        /// </summary>
        public bool HashCheck = true;
        //public int FtpPollFrequency = 5000;
    }

}
