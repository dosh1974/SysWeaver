using System;

namespace SysWeaver.ExchangeRate
{
    public sealed class ExchangeRateParams
    {
        /// <summary>
        /// Optionally specify where to store exchange rates on disc.
        /// Can use variables.
        /// Some common folder variables:
        ///             $(CommonApplicationData) = The directory that serves as a common repository for application-specific data that is used by all users.
        ///             $(LocalApplicationData) = The directory that serves as a common repository for application-specific data that is used by the current, non-roaming user.
        ///             $(ApplicationData) = The directory that serves as a common repository for application-specific data for the current roaming user (typically settings that should be shared between systems).
        ///             $(MyPictures) = The My Pictures folder.
        /// Env info variables:
        ///             $(ExeAppName) = Name of the executable.
        ///             $(Executable) = Full path to the executable.
        ///             $(ExecutableDir) = ExecutableDir.
        ///             $(ExecutableBase) = Full path to the folder where the application is.
        ///             $(AppInstance) = A "unique" id for this process
        /// </summary>
        public String CacheFolder;

        /// <summary>
        /// Can contain a map for some fictive symbol to a real symbol.
        /// Ex: "NLE=SLL".
        /// </summary>
        public String[] OldToNewRateMap = [
            "NLE=SLL"
            ];
    }

}
