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
        ///             $(AppName) = Application name.
        ///             $(Executable) = Full path to the executable.
        ///             $(ExecutableDir) = ExecutableDir.
        ///             $(ExecutableBase) = Full path to the folder where the application is.
        ///             $(Cc) = A "unique" id for this process
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
