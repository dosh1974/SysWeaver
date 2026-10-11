using System;
using System.Collections.Generic;

namespace SysWeaver.OsServices
{
    /// <summary>
    /// Result codes of service operations.
    /// Values less than or equal to zero are errors, <see cref="Ok"/> (1) and above are success or informational.
    /// Use <see cref="ServiceResponseHelper.Text"/> to get a description.
    /// NOTE: these values are NOT the process exit codes, <see cref="ServiceHost.Run"/> maps them using <see cref="ServiceResponseHelper.ToExitCode"/>
    /// (0 = success, non-zero = failure).
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
        /// The wrong number of arguments were supplied for the command (also used when too few arguments are supplied).
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
            { ServiceResponse.NotInstalled, "The service is not installed" },
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


        /// <summary>
        /// Exit code: success.
        /// </summary>
        public const int ExitSuccess = 0;
        /// <summary>
        /// Exit code: generic or unspecified error (LSB 1).
        /// </summary>
        public const int ExitGenericError = 1;
        /// <summary>
        /// Exit code: invalid or excess argument(s) (LSB 2).
        /// </summary>
        public const int ExitInvalidArguments = 2;
        /// <summary>
        /// Exit code: unimplemented feature, i.e. not supported by the service system or no service system for this OS (LSB 3).
        /// </summary>
        public const int ExitNotSupported = 3;
        /// <summary>
        /// Exit code: the service is not installed (LSB 5).
        /// </summary>
        public const int ExitNotInstalled = 5;
        /// <summary>
        /// Exit code: the service is not running / not in the required state (LSB 7).
        /// </summary>
        public const int ExitNotRunning = 7;
        /// <summary>
        /// Exit code: <see cref="ServiceResponse.InstallFailed"/> (LSB application range 150-199).
        /// </summary>
        public const int ExitInstallFailed = 150;
        /// <summary>
        /// Exit code: <see cref="ServiceResponse.UninstallFailed"/>.
        /// </summary>
        public const int ExitUninstallFailed = 151;
        /// <summary>
        /// Exit code: <see cref="ServiceResponse.StartFailed"/>.
        /// </summary>
        public const int ExitStartFailed = 152;
        /// <summary>
        /// Exit code: <see cref="ServiceResponse.StopFailed"/>.
        /// </summary>
        public const int ExitStopFailed = 153;
        /// <summary>
        /// Exit code: <see cref="ServiceResponse.PauseFailed"/>.
        /// </summary>
        public const int ExitPauseFailed = 154;
        /// <summary>
        /// Exit code: <see cref="ServiceResponse.ContinueFailed"/>.
        /// </summary>
        public const int ExitContinueFailed = 155;

        /// <summary>
        /// Get the process exit code used by <see cref="ServiceHost.Run"/> for the result of a verb.
        /// The convention is 0 = success and non-zero = failure (Windows, POSIX shells and systemd agree on this), the failure codes follow the
        /// LSB init script action codes where one exists, else the LSB application range (150-199); all codes are in 0-255 (Linux truncates exit codes to 8 bits).
        /// <list type="table">
        /// <item><term>0</term><description><see cref="ServiceResponse.Ok"/>, and the "already in the requested state" results: <see cref="ServiceResponse.AlreadyInstalled"/>,
        /// <see cref="ServiceResponse.AlreadyRunning"/>, <see cref="ServiceResponse.AlreadyStarting"/>, <see cref="ServiceResponse.AlreadyStopping"/>, <see cref="ServiceResponse.AlreadyPaused"/>,
        /// <see cref="ServiceResponse.NotRunning"/> for <see cref="ServiceVerbs.Stop"/>, <see cref="ServiceResponse.NotInstalled"/> / <see cref="ServiceResponse.NotFound"/> for <see cref="ServiceVerbs.Uninstall"/>.</description></item>
        /// <item><term>1</term><description><see cref="ServiceResponse.GenericError"/> (and undefined values).</description></item>
        /// <item><term>2</term><description><see cref="ServiceResponse.InvalidCommad"/>, <see cref="ServiceResponse.ToManyArgs"/>.</description></item>
        /// <item><term>3</term><description><see cref="ServiceResponse.NotSupported"/>, <see cref="ServiceResponse.UnhandledOs"/>.</description></item>
        /// <item><term>5</term><description><see cref="ServiceResponse.NotInstalled"/>, <see cref="ServiceResponse.NotFound"/> (except for uninstall).</description></item>
        /// <item><term>7</term><description><see cref="ServiceResponse.NotRunning"/> (except for stop), <see cref="ServiceResponse.NotPaused"/>.</description></item>
        /// <item><term>150-155</term><description><see cref="ServiceResponse.InstallFailed"/>, <see cref="ServiceResponse.UninstallFailed"/>, <see cref="ServiceResponse.StartFailed"/>,
        /// <see cref="ServiceResponse.StopFailed"/>, <see cref="ServiceResponse.PauseFailed"/>, <see cref="ServiceResponse.ContinueFailed"/>.</description></item>
        /// </list>
        /// </summary>
        /// <param name="response">The result of the verb.</param>
        /// <param name="verb">The verb that produced the result, decides if <see cref="ServiceResponse.NotRunning"/> and <see cref="ServiceResponse.NotInstalled"/> are a success
        /// (<see cref="ServiceVerbs.None"/> = no context, they are failures).</param>
        /// <returns>The exit code, 0 for success.</returns>
        public static int ToExitCode(this ServiceResponse response, ServiceVerbs verb = ServiceVerbs.None)
        {
            switch (response)
            {
                case ServiceResponse.Ok:
                case ServiceResponse.AlreadyInstalled:
                case ServiceResponse.AlreadyRunning:
                case ServiceResponse.AlreadyStarting:
                case ServiceResponse.AlreadyStopping:
                case ServiceResponse.AlreadyPaused:
                    return ExitSuccess;
                case ServiceResponse.NotRunning:
                    return verb == ServiceVerbs.Stop ? ExitSuccess : ExitNotRunning;
                case ServiceResponse.NotPaused:
                    return ExitNotRunning;
                case ServiceResponse.NotInstalled:
                case ServiceResponse.NotFound:
                    return verb == ServiceVerbs.Uninstall ? ExitSuccess : ExitNotInstalled;
                case ServiceResponse.InvalidCommad:
                case ServiceResponse.ToManyArgs:
                    return ExitInvalidArguments;
                case ServiceResponse.NotSupported:
                case ServiceResponse.UnhandledOs:
                    return ExitNotSupported;
                case ServiceResponse.InstallFailed:
                    return ExitInstallFailed;
                case ServiceResponse.UninstallFailed:
                    return ExitUninstallFailed;
                case ServiceResponse.StartFailed:
                    return ExitStartFailed;
                case ServiceResponse.StopFailed:
                    return ExitStopFailed;
                case ServiceResponse.PauseFailed:
                    return ExitPauseFailed;
                case ServiceResponse.ContinueFailed:
                    return ExitContinueFailed;
            }
            return ExitGenericError;
        }

        /// <summary>
        /// Get the (most likely) response from a process exit code created by <see cref="ToExitCode"/>, used to display the result of a verb that was run in an elevated process.
        /// The mapping is lossy: 0 => <see cref="ServiceResponse.Ok"/>, 2 => <see cref="ServiceResponse.InvalidCommad"/>, 3 => <see cref="ServiceResponse.NotSupported"/>,
        /// 5 => <see cref="ServiceResponse.NotInstalled"/>, 7 => <see cref="ServiceResponse.NotRunning"/>, 150-155 => the "Failed" responses, anything else => <see cref="ServiceResponse.GenericError"/>.
        /// </summary>
        /// <param name="exitCode">The process exit code.</param>
        /// <returns>The response.</returns>
        public static ServiceResponse FromExitCode(int exitCode)
        {
            switch (exitCode)
            {
                case ExitSuccess:
                    return ServiceResponse.Ok;
                case ExitInvalidArguments:
                    return ServiceResponse.InvalidCommad;
                case ExitNotSupported:
                    return ServiceResponse.NotSupported;
                case ExitNotInstalled:
                    return ServiceResponse.NotInstalled;
                case ExitNotRunning:
                    return ServiceResponse.NotRunning;
                case ExitInstallFailed:
                    return ServiceResponse.InstallFailed;
                case ExitUninstallFailed:
                    return ServiceResponse.UninstallFailed;
                case ExitStartFailed:
                    return ServiceResponse.StartFailed;
                case ExitStopFailed:
                    return ServiceResponse.StopFailed;
                case ExitPauseFailed:
                    return ServiceResponse.PauseFailed;
                case ExitContinueFailed:
                    return ServiceResponse.ContinueFailed;
            }
            return ServiceResponse.GenericError;
        }

        /// <summary>
        /// Find the result of a verb in the console output of a service host process (<see cref="ServiceHost.Run"/> prints it as the last line, using <see cref="Text"/>, ex: "All ok, operation is successful [Ok: 1]").
        /// Used to get the result independent of the exit code convention of the service host version (older versions exited with the <see cref="ServiceResponse"/> value).
        /// </summary>
        /// <param name="output">The (stdout) output of the process, may be null.</param>
        /// <param name="response">The response of the last line that ends with "[Name: Value]" where Name and Value are a defined <see cref="ServiceResponse"/>.</param>
        /// <returns>True if a response was found.</returns>
        public static bool TryParseOutput(String output, out ServiceResponse response)
        {
            response = ServiceResponse.GenericError;
            if (String.IsNullOrEmpty(output))
                return false;
            var lines = output.Split('\n');
            for (int i = lines.Length - 1; i >= 0; --i)
            {
                var l = lines[i].TrimEnd();
                if ((l.Length <= 0) || (l[l.Length - 1] != ']'))
                    continue;
                var s = l.LastIndexOf('[');
                if (s < 0)
                    continue;
                var kv = l.Substring(s + 1, l.Length - s - 2).Split(':');
                if (kv.Length != 2)
                    continue;
                var name = kv[0].Trim();
                if ((name.Length <= 0) || (!Char.IsLetter(name[0])))
                    continue;
                if (!Enum.TryParse(name, false, out ServiceResponse r))
                    continue;
                if (!int.TryParse(kv[1].Trim(), System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out var v))
                    continue;
                if (v != (int)r)
                    continue;
                response = r;
                return true;
            }
            return false;
        }

    }

}
