using System;

namespace SysWeaver.MicroService
{
    public sealed class FileLogParams
    {
        /// <summary>
        /// Optional filename for the log file (defaults to "$(ExecutableBase).log").
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
        public String Filename;
        /// <summary>
        /// How much detail to include in the log
        /// </summary>
        public Message.TextStyles Style = Message.TextStyles.Verbose;
        /// <summary>
        /// How to handle writing, 
        /// </summary>
        public MessageHandler.Modes Mode = MessageHandler.Modes.Async;
        /// <summary>
        /// The maximum size of the logfile, when exceeded it is truncated to half it's size.
        /// So reliable half of this size is available.
        /// </summary>
        public long MaxSize = 2 << 20;

        /// <summary>
        /// If true (the default), deleting the log file ("Api/logFile/DeleteLogFile") needs a one time token from "Api/logFile/GetDeleteLogFileToken" (valid for 5 minutes, for the same session),
        /// so the delete must be confirmed by the user (the page shows the facts and the warning of the token first) and a crafted link or a plain request can't delete the log.
        /// If false, a call without a token deletes the file (the behaviour before the token).
        /// </summary>
        public bool DeleteNeedsToken = true;

        /// <summary>
        /// If true, dump stats tables on exit.
        /// The extension of the file name is removed and then stats names are appended.
        /// </summary>
#if DEBUG
        public bool DumpStatsOnExit = true;
#else//DEBUG
        public bool DumpStatsOnExit;
#endif// DEBUG

    }

}
