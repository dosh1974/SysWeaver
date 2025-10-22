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
        ///             $(AppName) = Application name.
        ///             $(Executable) = Full path to the executable.
        ///             $(ExecutableDir) = ExecutableDir.
        ///             $(ExecutableBase) = Full path to the folder where the application is.
        ///             $(Cc) = A "unique" id for this process
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
    }

}
