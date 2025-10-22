using System;
using System.Collections.Generic;

namespace SysWeaver.OsServices
{
    public enum ServiceResponse
    {
        UnhandledOs = -12,

        InvalidCommad = -11,
        ToManyArgs = -10,

        ContinueFailed = -7,
        PauseFailed = -6,

        StopFailed = -5,
        StartFailed = -4,
        UninstallFailed = -3,
        InstallFailed = -2,
        NotFound = -1,

        GenericError = 0,
        Ok,
        AlreadyInstalled,
        AlreadyRunning,
        AlreadyStarting,
        AlreadyStopping,
        AlreadyPaused,
        NotRunning,
        NotInstalled,
        NotPaused,
        NotSupported,
    }

    public static class ServiceResponseHelper
    {
        static readonly IReadOnlyDictionary<ServiceResponse, String> IntTexts = new Dictionary<ServiceResponse, string>
        {
            { ServiceResponse.UnhandledOs , "The OS uses an unknown service system" },
            { ServiceResponse.InvalidCommad , "The command (verb) is invalid" },
            { ServiceResponse.ToManyArgs , "Too many arguments are supplied" },
            { ServiceResponse.ContinueFailed , "The service failed to continue (resume)" },
            { ServiceResponse.PauseFailed , "The service failed to pause" },
            { ServiceResponse.StopFailed , "The service failed to stop" },
            { ServiceResponse.StartFailed , "The service failed to start" },
            { ServiceResponse.UninstallFailed , "The service failed to be un-installed" },
            { ServiceResponse.InstallFailed , "The service installation failed" },
            { ServiceResponse.NotFound , "The service is not found (in the OS service system)" },
            { ServiceResponse.GenericError , "A generic error happened" },
            { ServiceResponse.Ok, "All ok, operation is successful" },
            { ServiceResponse.AlreadyInstalled, "The service is already installed" },
            { ServiceResponse.AlreadyRunning, "The service is already running" },
            { ServiceResponse.AlreadyStarting, "The service is already starting" },
            { ServiceResponse.AlreadyStopping, "The service is already stopping" },
            { ServiceResponse.AlreadyPaused, "The service is already paused" },
            { ServiceResponse.NotRunning, "The service is not running" },
            { ServiceResponse.NotInstalled, "The service in not installed" },
            { ServiceResponse.NotPaused, "The service is not paused" },
            { ServiceResponse.NotSupported, "The operation / command is not supported" },
        }.Freeze();

        public static String Text(this ServiceResponse status) => IntTexts.TryGetValue(status, out var t) ?
            String.Concat(t, " [", status, ": ", (int)status, ']')
            :
            String.Concat('[', status, ": ", (int)status, ']');

    }

}
