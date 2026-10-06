#pragma warning disable CA1416

namespace SysWeaver.OsServices.ServiceManager
{
    /// <summary>
    /// The current state of a Windows service (values 1-7 match the Win32 SERVICE_* states), plus <see cref="Unknown"/> and <see cref="NotFound"/>.
    /// </summary>
    enum ServiceState : int
    {
        /// <summary>
        /// The state cannot be (has not been) retrieved.
        /// </summary>
        Unknown = -1, // The state cannot be (has not been) retrieved.
        /// <summary>
        /// The service is not installed (couldn't be opened), also returned by waits that time out.
        /// </summary>
        NotFound = 0, // The service is not known on the host server.
        /// <summary>
        /// SERVICE_STOPPED, the service is stopped.
        /// </summary>
        Stop = 1, // The service is NET stopped.
        /// <summary>
        /// SERVICE_RUNNING, the service is running.
        /// </summary>
        Run = 4, // The service is NET started.
        /// <summary>
        /// SERVICE_STOP_PENDING, the service is stopping.
        /// </summary>
        Stopping = 3,
        /// <summary>
        /// SERVICE_START_PENDING, the service is starting.
        /// </summary>
        Starting = 2,

        /// <summary>
        /// Service is paused
        /// </summary>
        Paused = 7,
        /// <summary>
        /// Service is pausing
        /// </summary>
        Pausing = 6,
        
        /// <summary>
        /// Service is resuming from a pause
        /// </summary>
        Continuing = 5,
    }

}
