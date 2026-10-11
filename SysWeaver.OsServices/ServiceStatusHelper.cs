
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
        /// <param name="status">The status.</param>
        /// <returns>Ex: "The service is running [Running: 5]", for undefined values only the name and value, ex: "[99: 99]".</returns>
        public static String Text(this ServiceStatus status) => ((uint)status < (uint)IntTexts.Length) ?
            String.Concat(IntTexts[(int)status], " [", status, ": ", (int)status, ']')
            :
            String.Concat('[', status, ": ", (int)status, ']');


        /// <summary>
        /// The first exit code of the statuses that have no LSB status code, the exit code is this value plus the <see cref="ServiceStatus"/> value.
        /// </summary>
        public const int ExitCodeBase = 150;

        /// <summary>
        /// Get the process exit code used by <see cref="ServiceHost.Run"/> for the "status" verb, following the LSB init script "status" action codes where one exists:
        /// <list type="table">
        /// <item><term>0</term><description><see cref="ServiceStatus.Running"/> (LSB: program is running).</description></item>
        /// <item><term>3</term><description><see cref="ServiceStatus.Stopped"/> (LSB: program is not running).</description></item>
        /// <item><term>4</term><description><see cref="ServiceStatus.Unknown"/> and undefined values (LSB: status is unknown).</description></item>
        /// <item><term>150 + status</term><description>The others (LSB application range): <see cref="ServiceStatus.NotInstalled"/> = 151, <see cref="ServiceStatus.StartPending"/> = 153,
        /// <see cref="ServiceStatus.StopPending"/> = 154, <see cref="ServiceStatus.ContinuePending"/> = 156, <see cref="ServiceStatus.PausePending"/> = 157, <see cref="ServiceStatus.Paused"/> = 158.</description></item>
        /// </list>
        /// </summary>
        /// <param name="status">The status.</param>
        /// <returns>The exit code, 0 only for <see cref="ServiceStatus.Running"/>.</returns>
        public static int ToExitCode(this ServiceStatus status)
        {
            switch (status)
            {
                case ServiceStatus.Running:
                    return 0;
                case ServiceStatus.Stopped:
                    return 3;
                case ServiceStatus.NotInstalled:
                case ServiceStatus.StartPending:
                case ServiceStatus.StopPending:
                case ServiceStatus.ContinuePending:
                case ServiceStatus.PausePending:
                case ServiceStatus.Paused:
                    return ExitCodeBase + (int)status;
            }
            return 4;
        }

        /// <summary>
        /// Get the status from a process exit code created by <see cref="ToExitCode"/>.
        /// </summary>
        /// <param name="exitCode">The process exit code.</param>
        /// <returns>The status, <see cref="ServiceStatus.Unknown"/> for codes that aren't created by <see cref="ToExitCode"/>.</returns>
        public static ServiceStatus FromExitCode(int exitCode)
        {
            switch (exitCode)
            {
                case 0:
                    return ServiceStatus.Running;
                case 3:
                    return ServiceStatus.Stopped;
            }
            var s = (ServiceStatus)(exitCode - ExitCodeBase);
            if ((exitCode > ExitCodeBase) && (ToExitCode(s) == exitCode))
                return s;
            return ServiceStatus.Unknown;
        }

    }

}
