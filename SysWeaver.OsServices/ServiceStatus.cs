namespace SysWeaver.OsServices
{
    /// <summary>
    /// The status of an OS service, use <see cref="ServiceStatusHelper.Text"/> to get a description.
    /// </summary>
    public enum ServiceStatus
    {
        /// <summary>
        /// The status is unknown (or couldn't be determined).
        /// </summary>
        Unknown,
        /// <summary>
        /// The service is not installed.
        /// </summary>
        NotInstalled,
        /// <summary>
        /// The service is installed but not running.
        /// </summary>
        Stopped,
        /// <summary>
        /// The service is starting.
        /// </summary>
        StartPending,
        /// <summary>
        /// The service is stopping.
        /// </summary>
        StopPending,
        /// <summary>
        /// The service is running.
        /// </summary>
        Running,
        /// <summary>
        /// The service is resuming after a pause.
        /// </summary>
        ContinuePending,
        /// <summary>
        /// The service is pausing.
        /// </summary>
        PausePending,
        /// <summary>
        /// The service is paused.
        /// </summary>
        Paused,
    }

}
