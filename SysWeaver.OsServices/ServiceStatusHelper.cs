
using System;

namespace SysWeaver.OsServices
{
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


        public static String Text(this ServiceStatus status) => String.Concat(IntTexts[(int)status], " [", status, ": ", (int)status, ']');

    }

}
