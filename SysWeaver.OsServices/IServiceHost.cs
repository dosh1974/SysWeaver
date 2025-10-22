
using System;
using System.Runtime.InteropServices;
using SysWeaver.MicroService;

namespace SysWeaver.OsServices
{

    public interface IServiceHost
    {

        /// <summary>
        /// The display name of this service system 
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
        /// Run as a daemon (main function of a daemon process)
        /// </summary>
        /// <param name="onStart">Optional callback to execute after all services in the manifest file have been created</param>
        /// <returns>The exit code</returns>
        int Run(Action<ServiceManager> onStart);

        /// <summary>
        /// Return status of the service
        /// </summary>
        /// <returns></returns>
        ServiceStatus Status();

        /// <summary>
        /// Installs the service (no start)
        /// </summary>
        /// <returns></returns>
        ServiceResponse Install();

        /// <summary>
        /// (Stops and) uninstalls the service
        /// </summary>
        /// <returns></returns>
        ServiceResponse Uninstall();

        /// <summary>
        /// (Install and) start the service
        /// </summary>
        /// <returns></returns>
        ServiceResponse Start();

        /// <summary>
        /// Stop the service
        /// </summary>
        /// <returns></returns>
        ServiceResponse Stop();

        /// <summary>
        /// Pause a running service
        /// </summary>
        /// <returns></returns>
        ServiceResponse Pause();

        /// <summary>
        /// Resume a paused service
        /// </summary>
        /// <returns></returns>
        ServiceResponse Continue();

    }

}
