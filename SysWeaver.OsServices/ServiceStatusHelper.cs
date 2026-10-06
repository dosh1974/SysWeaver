
using System;

namespace SysWeaver.OsServices
{
    /// <summary>
    /// Extensions for <see cref="ServiceStatus"/>.
    /// </summary>
    public static class ServiceStatusHelper
    {
        static readonly String[] IntTexts = 
        [
            "Unknown",
            "The service is not installed",
            "The service isn't running",
            "The service is starting up",
            "The service is stopping",
            "The service is running",
            "The service is resuming after a pause",
            "The service is pausing",
            "The service is paused",
        ];


        /// <summary>
        /// Get a human readable description of a status, including the name and numeric value.
        /// </summary>
        /// <param name="status">The status, must be a defined value.</param>
        /// <returns>Ex: "The service is running [Running: 5]".</returns>
        /// <exception cref="IndexOutOfRangeException"><paramref name="status"/> isn't a defined value.</exception>
        public static String Text(this ServiceStatus status) => String.Concat(IntTexts[(int)status], " [", status, ": ", (int)status, ']');

    }

}
