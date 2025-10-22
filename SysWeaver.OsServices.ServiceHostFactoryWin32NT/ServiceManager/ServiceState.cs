#pragma warning disable CA1416

namespace SysWeaver.OsServices.ServiceManager
{
    /// <summary>
    /// 
    /// </summary>
    enum ServiceState : int
    {
        /// <summary>
        /// 
        /// </summary>
        Unknown = -1, // The state cannot be (has not been) retrieved.
        /// <summary>
        /// 
        /// </summary>
        NotFound = 0, // The service is not known on the host server.
        /// <summary>
        /// 
        /// </summary>
        Stop = 1, // The service is NET stopped.
        /// <summary>
        /// 
        /// </summary>
        Run = 4, // The service is NET started.
        /// <summary>
        /// 
        /// </summary>
        Stopping = 3,
        /// <summary>
        /// 
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
