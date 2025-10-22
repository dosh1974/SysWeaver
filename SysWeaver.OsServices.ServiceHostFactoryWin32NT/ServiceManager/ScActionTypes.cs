#pragma warning disable CA1416

namespace SysWeaver.OsServices.ServiceManager
{
    enum ScActionTypes : uint
    {
        /// <summary>
        /// No action.
        /// </summary>
        SC_ACTION_NONE = 0,
        /// <summary>
        /// Reboot the computer.
        /// </summary>
        SC_ACTION_REBOOT = 2,
        /// <summary>
        /// Restart the service.
        /// </summary>
        SC_ACTION_RESTART = 1,
        /// <summary>
        /// Run a command.
        /// </summary>
        SC_ACTION_RUN_COMMAND = 3
    }

}
