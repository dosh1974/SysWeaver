using System;
using System.Collections.Generic;

namespace SysWeaver.OsServices
{
    /// <summary>
    /// Result codes of service operations, also used as the process exit code by <see cref="ServiceHost.Run"/>.
    /// Values less than or equal to zero are errors, <see cref="Ok"/> (1) and above are success or informational.
    /// Use <see cref="ServiceResponseHelper.Text"/> to get a description.
    /// </summary>
    public enum ServiceResponse
    {
        /// <summary>
        /// The OS uses an unknown / unsupported service system.
        /// </summary>
        UnhandledOs = -12,

        /// <summary>
        /// The command (verb) is invalid.
        /// </summary>
        InvalidCommad = -11,
        /// <summary>
        /// The wrong number of arguments were supplied for the command.
        /// </summary>
        ToManyArgs = -10,

        /// <summary>
        /// The service failed to continue (resume).
        /// </summary>
        ContinueFailed = -7,
        /// <summary>
        /// The service failed to pause.
        /// </summary>
        PauseFailed = -6,

        /// <summary>
        /// The service failed to stop.
        /// </summary>
        StopFailed = -5,
        /// <summary>
        /// The service failed to start.
        /// </summary>
        StartFailed = -4,
        /// <summary>
        /// The service failed to be uninstalled.
        /// </summary>
        UninstallFailed = -3,
        /// <summary>
        /// The service installation failed.
        /// </summary>
        InstallFailed = -2,
        /// <summary>
        /// The service is not found in the OS service system.
        /// </summary>
        NotFound = -1,

        /// <summary>
        /// A generic error happened (also returned when an exception is caught).
        /// </summary>
        GenericError = 0,
        /// <summary>
        /// All ok, the operation is successful.
        /// </summary>
        Ok,
        /// <summary>
        /// The service is already installed.
        /// </summary>
        AlreadyInstalled,
        /// <summary>
        /// The service is already running.
        /// </summary>
        AlreadyRunning,
        /// <summary>
        /// The service is already starting.
        /// </summary>
        AlreadyStarting,
        /// <summary>
        /// The service is already stopping.
        /// </summary>
        AlreadyStopping,
        /// <summary>
        /// The service is already paused.
        /// </summary>
        AlreadyPaused,
        /// <summary>
        /// The service is not running.
        /// </summary>
        NotRunning,
        /// <summary>
        /// The service is not installed.
        /// </summary>
        NotInstalled,
        /// <summary>
        /// The service is not paused.
        /// </summary>
        NotPaused,
        /// <summary>
        /// The operation / command is not supported (by this service system).
        /// </summary>
        NotSupported,
    }

    /// <summary>
    /// Extensions for <see cref="ServiceResponse"/>.
    /// </summary>
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

        /// <summary>
        /// Get a human readable description of a response, including the name and numeric value.
        /// </summary>
        /// <param name="status">The response.</param>
        /// <returns>Ex: "All ok, operation is successful [Ok: 1]".</returns>
        public static String Text(this ServiceResponse status) => IntTexts.TryGetValue(status, out var t) ?
            String.Concat(t, " [", status, ": ", (int)status, ']')
            :
            String.Concat('[', status, ": ", (int)status, ']');

    }

}
