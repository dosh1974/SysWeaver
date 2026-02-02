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
    }

}
