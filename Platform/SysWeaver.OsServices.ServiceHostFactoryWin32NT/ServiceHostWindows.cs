using SysWeaver.OsServices.ServiceManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Threading;

#pragma warning disable CA1416

namespace SysWeaver.OsServices
{

    /// <summary>
    /// <see cref="IServiceHost"/> for the Windows Service Control Manager.
    /// The service is registered as an own process service running "[command] daemon" (as LocalSystem), with description, startup type
    /// (<see cref="ServiceParams.Start"/>) and optional restart on failure actions (<see cref="ServiceParams.RestartOnFail"/>).
    /// </summary>
    sealed class ServiceHostWindows : IServiceHost
    {

        /// <inheritdoc/>
        public String Name => "Windows service manager";

        /// <summary>
        /// Creates the host.
        /// </summary>
        /// <param name="p">The service parameters.</param>
        public ServiceHostWindows(ServiceParams p)
        {
            P = p;
            IsElevated = ElevatedProcessWin32NT.IsElevated;
        }

        readonly ServiceParams P;


        /// <summary>
        /// Poll the service state (every 100 ms) until it's one of <paramref name="states"/>, or (after 3 seconds) no longer <paramref name="whileStateIs"/>.
        /// Gives up after 3 minutes.
        /// </summary>
        /// <param name="name">The service name.</param>
        /// <param name="whileStateIs">The transitional state to wait on.</param>
        /// <param name="states">The states to wait for.</param>
        /// <returns>The last state, <see cref="SysWeaver.OsServices.ServiceManager.ServiceState.NotFound"/> on time out.</returns>
        static ServiceState WaitForWhile(String name, ServiceState whileStateIs, params ServiceState[] states)
        {
            var s = new HashSet<ServiceState>(states);
            var checkWhile = DateTime.UtcNow + TimeSpan.FromSeconds(3);
            var end = DateTime.UtcNow + TimeSpan.FromSeconds(60 * 3);
            for (; ; )
            {
                var t = Win32ServiceManager.GetServiceStatus(name);
                if (t == ServiceState.NotFound)
                    return t;
                if (t == ServiceState.Unknown)
                    return t;
                if (s.Contains(t))
                    return t;
                var now = DateTime.UtcNow;
                if (now > checkWhile)
                {
                    if (t != whileStateIs)
                        return t;
                }
                if (DateTime.UtcNow > end)
                    return ServiceState.NotFound;
                Thread.Sleep(100);
            }
        }

        /// <summary>
        /// Poll the service state (every 100 ms) until it's one of <paramref name="states"/>, gives up after 60 seconds.
        /// </summary>
        /// <param name="name">The service name.</param>
        /// <param name="states">The states to wait for.</param>
        /// <returns>The last state, <see cref="SysWeaver.OsServices.ServiceManager.ServiceState.NotFound"/> on time out.</returns>
        static ServiceState WaitFor(String name, params ServiceState[] states)
        {
            var s = new HashSet<ServiceState>(states);
            var end = DateTime.UtcNow + TimeSpan.FromSeconds(60);
            for (; ;)
            {
                var t = Win32ServiceManager.GetServiceStatus(name);
                if (t == ServiceState.NotFound)
                    return t;
                if (t == ServiceState.Unknown)
                    return t;
                if (s.Contains(t))
                    return t;
                if (DateTime.UtcNow > end)
                    return ServiceState.NotFound;
                Thread.Sleep(100);
            }
        }

        /// <summary>
        /// Run the service using <see cref="ServiceBase.Run(ServiceBase)"/>, blocks until the SCM stops the service.
        /// </summary>
        /// <param name="onStart">Optional callback to execute after all services in the manifest file have been created.</param>
        /// <returns>Always 0.</returns>
        public int Run(Action<SysWeaver.MicroService.ServiceManager> onStart)
        {
            using var inst = new ServiceInstance(P, onStart);
            ServiceBase.Run(inst);
            return 0;
        }


        #region Elevation

        /// <inheritdoc/>
        public bool IsElevated { get; private set; }

        static readonly IReadOnlySet<ServiceVerbs> IntNeedElevation = ReadOnlyData.Set(
            ServiceVerbs.Install,
            ServiceVerbs.Uninstall,
            ServiceVerbs.Reinstall,
            ServiceVerbs.Restart,
            ServiceVerbs.Start,
            ServiceVerbs.Stop,
            ServiceVerbs.Pause,
            ServiceVerbs.Continue
        );

        /// <inheritdoc/>
        public bool NeedElevation(ServiceVerbs verb) => IntNeedElevation.Contains(verb);

        /// <inheritdoc cref="ElevatedProcessWin32NT.RunElevated(string, bool, bool)"/>
        public int RunElevated(String commandLine, bool terminal, bool noWait) => ElevatedProcessWin32NT.RunElevated(commandLine, terminal, noWait);

        #endregion//Elevation

        /// <summary>
        /// Get the status by opening the service (by service name, case insensitive) with query status rights only (<see cref="Win32ServiceManager.GetServiceStatus(string)"/>), doesn't need elevation.
        /// </summary>
        /// <returns>The status, <see cref="ServiceStatus.NotInstalled"/> if the service can't be opened (no service with the name exists, or access is denied),
        /// <see cref="ServiceStatus.Unknown"/> if the SCM or the status couldn't be queried.</returns>
        public ServiceStatus Status()
        {
            ServiceState s;
            try
            {
                s = Win32ServiceManager.GetServiceStatus(P.Name);
            }
            catch
            {
                return ServiceStatus.Unknown;
            }
            return ToServiceStatus(s);
        }

        /// <summary>
        /// Map a Windows service state to a <see cref="ServiceStatus"/>.
        /// </summary>
        /// <param name="s">The state.</param>
        /// <returns>The status, <see cref="ServiceStatus.NotInstalled"/> for <see cref="SysWeaver.OsServices.ServiceManager.ServiceState.NotFound"/>, <see cref="ServiceStatus.Unknown"/> for undefined values.</returns>
        static ServiceStatus ToServiceStatus(ServiceState s)
        {
            switch (s)
            {
                case ServiceState.NotFound:
                    return ServiceStatus.NotInstalled;
                case ServiceState.Stop:
                    return ServiceStatus.Stopped;
                case ServiceState.Starting:
                    return ServiceStatus.StartPending;
                case ServiceState.Stopping:
                    return ServiceStatus.StopPending;
                case ServiceState.Run:
                    return ServiceStatus.Running;
                case ServiceState.Continuing:
                    return ServiceStatus.ContinuePending;
                case ServiceState.Pausing:
                    return ServiceStatus.PausePending;
                case ServiceState.Paused:
                    return ServiceStatus.Paused;
            }
            return ServiceStatus.Unknown;
        }


        /// <inheritdoc/>
        public ServiceResponse Install()
        {
            var p = P;
            String name = p.Name;
            if (Win32ServiceManager.IsInstalled(name))
                return ServiceResponse.AlreadyInstalled;
            var cmd = ServiceHost.GetCommand("daemon");
            if (!Win32ServiceManager.Install(name, p.DisplayName, cmd, true))
                return ServiceResponse.InstallFailed;
            Win32ServiceManager.SetDescription(name, p.Description ?? "", true);
            if (p.RestartOnFail)
                Win32ServiceManager.EnableRestartServiceOnError(name, true, p.RestartDelaySeconds, p.RestartDelayLastSeconds, p.ResetSeconds);
            switch (p.Start)
            {
                case ServiceStarts.Disabled:
                    Win32ServiceManager.SetStartupType(name, StartTypes.SERVICE_DISABLED);
                    break;
                case ServiceStarts.Manual:
                    Win32ServiceManager.SetStartupType(name, StartTypes.SERVICE_DEMAND_START);
                    break;
                case ServiceStarts.Normal:
                    Win32ServiceManager.SetStartupType(name, StartTypes.SERVICE_AUTO_START);
                    break;
                case ServiceStarts.Delayed:
                    Win32ServiceManager.SetStartupType(name, StartTypes.SERVICE_AUTO_START);
                    Win32ServiceManager.SetDelayedStart(name, true, true);
                    break;
            }
            if (WaitFor(name, ServiceState.Stop) == ServiceState.Stop)
                return ServiceResponse.Ok;
            return ServiceResponse.GenericError;
        }

        /// <inheritdoc/>
        public ServiceResponse Uninstall()
        {
            var p = P;
            String name = p.Name;
            if (!Win32ServiceManager.IsInstalled(name))
                return ServiceResponse.NotInstalled;
            var c = Stop();
            switch (c)
            {
                case ServiceResponse.Ok:
                case ServiceResponse.NotRunning:
                    break;
                default:
                    return c;
            }
            try
            {
                Win32ServiceManager.Uninstall(name);
                return ServiceResponse.Ok;
            }
            catch
            {
            }
            return ServiceResponse.UninstallFailed;
        }



        /// <summary>
        /// Install (if needed) and start the service. NOTE: a paused service is not resumed (returns <see cref="ServiceResponse.StartFailed"/>).
        /// </summary>
        /// <returns>The result.</returns>
        public ServiceResponse Start()
        {
        //  Install
            var c = Install();
            switch (c)
            {
                case ServiceResponse.AlreadyInstalled:
                case ServiceResponse.Ok:
                    break;
                default:
                    return c;
            }
            //  Start
            var p = P;
            String name = p.Name;
            var status = Win32ServiceManager.GetServiceStatus(name);
            if (status == ServiceState.NotFound)
                return ServiceResponse.InstallFailed;
            if (status == ServiceState.Unknown)
                return ServiceResponse.GenericError;
            if (status == ServiceState.Run)
                return ServiceResponse.AlreadyRunning;
            if (status == ServiceState.Starting)
            {
                if (WaitForWhile(name, ServiceState.Starting, ServiceState.Run) == ServiceState.Run)
                    return ServiceResponse.AlreadyStarting;
            }
            try
            {
                Win32ServiceManager.Start(name);
                if (WaitForWhile(name, ServiceState.Starting, ServiceState.Run) == ServiceState.Run)
                    return ServiceResponse.Ok;
            }
            catch
            {
            }
            return ServiceResponse.StartFailed;
        }


        /// <summary>
        /// Stop the service and wait (up to 3 minutes) until it's stopped.
        /// A starting or continuing service is first waited on; returns <see cref="ServiceResponse.NotRunning"/> if the service is stopped (or was stopping and stopped),
        /// <see cref="ServiceResponse.NotFound"/> if it doesn't exist (never <see cref="ServiceResponse.AlreadyStopping"/>).
        /// </summary>
        /// <returns>The result.</returns>
        public ServiceResponse Stop()
        {
            var p = P;
            String name = p.Name;
            var status = Win32ServiceManager.GetServiceStatus(name);
            if (status == ServiceState.NotFound)
                return ServiceResponse.NotFound;
            if (status == ServiceState.Unknown)
                return ServiceResponse.GenericError;
            if (status == ServiceState.Stop)
                return ServiceResponse.NotRunning;
            if (status == ServiceState.Stopping)
            {
                if (WaitForWhile(name, ServiceState.Stopping, ServiceState.Stop) == ServiceState.Stop)
                    return ServiceResponse.NotRunning;
            }
            if (status == ServiceState.Starting)
                if (WaitForWhile(name, ServiceState.Starting, ServiceState.Run) != ServiceState.Run)
                    return ServiceResponse.NotRunning;
            if (status == ServiceState.Continuing)
                if (WaitForWhile(name, ServiceState.Continuing, ServiceState.Run) != ServiceState.Run)
                    return ServiceResponse.NotRunning;
            try
            {
                Win32ServiceManager.Stop(name);
                if (WaitForWhile(name, ServiceState.Stopping, ServiceState.Stop) == ServiceState.Stop)
                    return ServiceResponse.Ok;
            }
            catch
            {
            }
            return ServiceResponse.StopFailed;
        }

        /// <summary>
        /// Pause the running service and wait (up to 3 minutes) until it's paused.
        /// Returns <see cref="ServiceResponse.AlreadyPaused"/> if the service is paused, <see cref="ServiceResponse.NotRunning"/> if it's in any other state than running,
        /// <see cref="ServiceResponse.NotFound"/> if it doesn't exist.
        /// </summary>
        /// <returns>The result.</returns>
        public ServiceResponse Pause()
        {
            //  Start
            var p = P;
            String name = p.Name;
            var status = Win32ServiceManager.GetServiceStatus(name);
            if (status == ServiceState.NotFound)
                return ServiceResponse.NotFound;
            if (status == ServiceState.Unknown)
                return ServiceResponse.GenericError;
            if (status == ServiceState.Paused)
                return ServiceResponse.AlreadyPaused;
            if (status != ServiceState.Run)
                return ServiceResponse.NotRunning;
            try
            {
                Win32ServiceManager.Pause(name);
                if (WaitForWhile(name, ServiceState.Pausing, ServiceState.Paused) == ServiceState.Paused)
                    return ServiceResponse.Ok;
            }
            catch
            {
            }
            return ServiceResponse.PauseFailed;
        }

        /// <summary>
        /// Resume the paused service and wait (up to 3 minutes) until it's running.
        /// Returns <see cref="ServiceResponse.AlreadyRunning"/> if the service is running, <see cref="ServiceResponse.NotPaused"/> if it's in any other state than paused,
        /// <see cref="ServiceResponse.NotFound"/> if it doesn't exist.
        /// </summary>
        /// <returns>The result.</returns>
        public ServiceResponse Continue()
        {
            //  Start
            var p = P;
            String name = p.Name;
            var status = Win32ServiceManager.GetServiceStatus(name);
            if (status == ServiceState.NotFound)
                return ServiceResponse.NotFound;
            if (status == ServiceState.Unknown)
                return ServiceResponse.GenericError;
            if (status == ServiceState.Run)
                return ServiceResponse.AlreadyRunning;
            if (status != ServiceState.Paused)
                return ServiceResponse.NotPaused;
            try
            {
                Win32ServiceManager.Continue(name);
                if (WaitForWhile(name, ServiceState.Continuing, ServiceState.Run) == ServiceState.Run)
                    return ServiceResponse.Ok;
            }
            catch
            {
            }
            return ServiceResponse.ContinueFailed;
        }

    }
}
