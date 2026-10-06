
using System;
using System.Runtime.InteropServices;
using SysWeaver.MicroService;

namespace SysWeaver.OsServices
{

    /// <summary>
    /// OS specific service system (Windows Service Control Manager, systemd, SysVinit) used by <see cref="ServiceHost.Run"/> to install, control and run the application as a service.
    /// Instances are created by an <see cref="IServiceHostFactory"/> implementation in an OS specific factory assembly.
    /// </summary>
    public interface IServiceHost
    {

        /// <summary>
        /// The display name of this service system (ex: "Windows Service Control Manager", "systemd")
        /// </summary>
        String Name { get; }    

        /// <summary>
        /// True if the process is running as elevated, else false
        /// </summary>
        bool IsElevated { get; }

        /// <summary>
        /// Returns true if the verb requires elevation, else false
        /// </summary>
        /// <param name="verb">The verb to test</param>
        /// <returns>True if elevation is required, else false</returns>
        bool NeedElevation(ServiceVerbs verb);


        /// <summary>
        /// Run the command elevated (super-user), wait for the process to exit and return the process exit code.
        /// </summary>
        /// <param name="commandLine">The command line to run</param>
        /// <param name="terminal">If true, run in console, else run hidden</param>
        /// <param name="noWait">If true, start the process and return directly</param>
        /// <returns>The exit code of the command or 0 if noWait is true</returns>
        int RunElevated(String commandLine, bool terminal, bool noWait);


        /// <summary>
        /// Run as a daemon (main function of a daemon process), blocks until the OS service system stops the service.
        /// </summary>
        /// <param name="onStart">Optional callback to execute after all services in the manifest file have been created</param>
        /// <returns>The process exit code (the built-in hosts return 0 when the service stopped normally)</returns>
        int Run(Action<ServiceManager> onStart);

        /// <summary>
        /// Return status of the service
        /// </summary>
        /// <returns>The current status</returns>
        ServiceStatus Status();

        /// <summary>
        /// Installs the service (no start)
        /// </summary>
        /// <returns>The result, ex: <see cref="ServiceResponse.Ok"/> or <see cref="ServiceResponse.AlreadyInstalled"/></returns>
        ServiceResponse Install();

        /// <summary>
        /// (Stops and) uninstalls the service
        /// </summary>
        /// <returns>The result, ex: <see cref="ServiceResponse.Ok"/> or <see cref="ServiceResponse.NotInstalled"/></returns>
        ServiceResponse Uninstall();

        /// <summary>
        /// (Install and) start the service
        /// </summary>
        /// <returns>The result, ex: <see cref="ServiceResponse.Ok"/> or <see cref="ServiceResponse.AlreadyRunning"/></returns>
        ServiceResponse Start();

        /// <summary>
        /// Stop the service
        /// </summary>
        /// <returns>The result, ex: <see cref="ServiceResponse.Ok"/> or <see cref="ServiceResponse.NotRunning"/></returns>
        ServiceResponse Stop();

        /// <summary>
        /// Pause a running service
        /// </summary>
        /// <returns>The result, ex: <see cref="ServiceResponse.Ok"/> or <see cref="ServiceResponse.NotSupported"/></returns>
        ServiceResponse Pause();

        /// <summary>
        /// Resume a paused service
        /// </summary>
        /// <returns>The result, ex: <see cref="ServiceResponse.Ok"/> or <see cref="ServiceResponse.NotPaused"/></returns>
        ServiceResponse Continue();

    }

}
