#pragma warning disable CA1416

namespace SysWeaver.OsServices.ServiceManager
{
    enum ServiceStates : int
    {

        /// <summary>
        /// The service is about to continue.
        /// </summary>
        SERVICE_CONTINUE_PENDING = 0x00000005,
        /// <summary>
        /// The service is pausing.
        /// </summary>
        SERVICE_PAUSE_PENDING = 0x00000006,
        /// <summary>
        /// The service is paused.
        /// </summary>
        SERVICE_PAUSED = 0x00000007,
        /// <summary>
        /// The service is running.
        /// </summary>
        SERVICE_RUNNING = 0x00000004,
        /// <summary>
        /// The service is starting.
        /// </summary>
        SERVICE_START_PENDING = 0x00000002,
        /// <summary>
        /// The service is stopping.
        /// </summary>
        SERVICE_STOP_PENDING = 0x00000003,
        /// <summary>
        /// The service has stopped.
        /// </summary>
        SERVICE_STOPPED = 0x00000001,
    }

}
