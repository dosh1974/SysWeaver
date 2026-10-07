using System;
using System.Runtime.InteropServices;

#pragma warning disable CA1416

namespace SysWeaver.OsServices.ServiceManager
{

    /// <summary>
    /// Installs and provides functionality for handling windows services using the advapi32 Service Control Manager API.
    /// </summary>
    /// <remarks>
    /// Most operations require administrator rights. Each call opens and closes its own SCM and service handles.
    /// Success is determined from the return values of the API calls, error codes are read using <see cref="Marshal.GetLastWin32Error"/>.
    /// </remarks>
    static class Win32ServiceManager
    {

        /// <summary>
        /// Get the description of a service
        /// </summary>
        /// <param name="serviceName">The windows service name to get the description for</param>
        /// <param name="failSilent">If true, return null on error, else throw exceptions</param>
        /// <returns>The description of the supplied service</returns>
        public static string GetDescription(string serviceName, bool failSilent = false)
        {
            var t = GetServiceConfig2<ServiceDescription>(serviceName, SERVICE_CONFIG_DESCRIPTION, failSilent);
            return t?.lpDescription;
        }

        /// <summary>
        /// Set the description of a service
        /// </summary>
        /// <param name="serviceName">The windows service name to set the description for</param>
        /// <param name="description">The new description</param>
        /// <param name="failSilent">If true, return false on error, else throw exceptions</param>
        /// <returns>True if succeeded</returns>
        public static bool SetDescription(string serviceName, string description, bool failSilent = false)
        {
            var t = new ServiceDescription
            {
                lpDescription = description
            };
            return SetServiceConfig2(serviceName, SERVICE_CONFIG_DESCRIPTION, t, failSilent);
        }

        /// <summary>
        /// Configure the failure actions of a service to restart it on the first, second and subsequent failures.
        /// </summary>
        /// <param name="serviceName">The windows service name to enable restart on fail</param>
        /// <param name="failSilent">If true, return false on error, else throw exceptions</param>
        /// <param name="restartDelaySeconds">Number of seconds to wait on first and second fails</param>
        /// <param name="restartDelayLastSeconds">Number of seconds to wait on the third (and later) fail</param>
        /// <param name="resetSeconds">Number of seconds before resetting the failure counter</param>
        /// <returns>True if succeeded</returns>
        public static bool EnableRestartServiceOnError(string serviceName, bool failSilent = false, int restartDelaySeconds = 2 * 60, int restartDelayLastSeconds = 5 * 60, int resetSeconds = 24 * 60 * 60)
        {
            var t0 = (uint)Math.Max(1, restartDelaySeconds) * 1000;
            var t1 = (uint)Math.Max(1, restartDelayLastSeconds) * 1000;
            var t2 = Math.Max(1, resetSeconds);
            ScAction[] actions = 
                [
                    new ScAction
                    {
                        Type = ScActionTypes.SC_ACTION_RESTART,
                        Delay = t0,
                    },
                    new ScAction
                    {
                        Type = ScActionTypes.SC_ACTION_RESTART,
                        Delay = t0,
                    },
                    new ScAction
                    {
                        Type = ScActionTypes.SC_ACTION_RESTART,
                        Delay = t1,
                    },
                ];
            var pa = GCHandle.Alloc(actions, GCHandleType.Pinned);
            try
            {
                var t = new ServiceFailureActions
                {
                    dwResetPeriod = t2,
                    lpCommand = null,
                    lpRebootMsg = null,
                    cActions = actions.Length,
                    lpsaActions = pa.AddrOfPinnedObject(),
                };
                return SetServiceConfig2(serviceName, SERVICE_CONFIG_FAILURE_ACTIONS, t, failSilent);

            }
            finally
            {
                pa.Free();
            }
        }

        /// <summary>
        /// Get a flag indicating if the (auto start) service has a delayed start or not
        /// </summary>
        /// <param name="isDelayed">True if the service start-up is delayed</param>
        /// <param name="serviceName">The windows service name to get information for</param>
        /// <param name="failSilent">If true, return false on error, else throw exceptions</param>
        /// <returns>True if succeeded</returns>
        public static bool GetDelayedStart(out bool isDelayed, string serviceName, bool failSilent = false)
        {
            var t = GetServiceConfig2<ServiceDelayedAutoStartInfo>(serviceName, SERVICE_CONFIG_DELAYED_AUTO_START_INFO, failSilent);
            isDelayed = t?.fDelayedAutostart ?? false;
            return t != null;
        }

        /// <summary>
        /// Enable or disable delayed start of an auto start service
        /// </summary>
        /// <param name="serviceName">The windows service name to change</param>
        /// <param name="isDelayed">True to enable delayed start of the service, else false</param>
        /// <param name="failSilent">If true, return false on error, else throw exceptions</param>
        /// <returns>True if succeeded</returns>
        public static bool SetDelayedStart(string serviceName, bool isDelayed = true, bool failSilent = false)
        {
            var t = new ServiceDelayedAutoStartInfo
            {
                fDelayedAutostart = isDelayed,
            };
            return SetServiceConfig2(serviceName, SERVICE_CONFIG_DELAYED_AUTO_START_INFO, t, failSilent);
        }

        const String DeletedDesc = "** Deleted **";

        /// <summary>
        /// Marks a windows service for deletion (it is NOT stopped first, the SCM deletes it once it has stopped and all handles are closed).
        /// The description is temporarily set to "** Deleted **" so that <see cref="IsInstalled"/> treats a service pending deletion as not installed.
        /// </summary>
        /// <param name="serviceName">The windows service name to uninstall</param>
        /// <exception cref="ApplicationException">The SCM couldn't be opened, or the service couldn't be opened or deleted.</exception>
        public static void Uninstall(string serviceName)
        {
            nint scman = OpenSCManager(ServiceManagerRights.Connect);
            try
            {
                nint service = OpenService(scman, serviceName,
                ServiceRights.StandardRightsRequired | ServiceRights.Stop |
                ServiceRights.QueryStatus | ServiceRights.Delete | ServiceRights.QueryConfig | ServiceRights.ChangeConfig);
                if (service == nint.Zero)
                {
                    var le = GetLastError();
                    throw new ApplicationException(String.Concat("Could not open service \"", serviceName, "\", error: ", le, " (0x", le.ToString("x").PadLeft(8, '0'), ')'));
                }
                try
                {
                    //StopService(service);
                    var desc = GetDescription(service, true);
                    SetDescription(service, DeletedDesc, true);
                    int ret = DeleteService(service);
                    if (ret == 0)
                    {
                        var le = GetLastError();
                        if (desc != null)
                            SetDescription(service, desc, true);
                        throw new ApplicationException(String.Concat("Could not delete service \"", serviceName, "\", error: ", le, " (0x", le.ToString("x").PadLeft(8, '0'), ')'));
                    }
                }
                finally
                {
                    CloseServiceHandle(service);
                }
            }
            finally
            {
                CloseServiceHandle(scman);
            }
        }

        /// <summary>
        /// Accepts a service name and returns true if the service with that service name exists (and isn't marked as deleted by <see cref="Uninstall"/>)
        /// </summary>
        /// <param name="serviceName">The service name that we will check for existence</param>
        /// <returns>True if that service exists false otherwise</returns>
        /// <exception cref="ApplicationException">The SCM couldn't be opened.</exception>
        public static bool IsInstalled(string serviceName)
        {
            nint scman = OpenSCManager(ServiceManagerRights.Connect);
            try
            {
                nint service = OpenService(scman, serviceName, ServiceRights.QueryStatus | ServiceRights.QueryConfig);
                if (service == nint.Zero)
                    return false;
                try
                {
                    if (GetDescription(service, true) == DeletedDesc)
                        return false;
                }
                catch
                {
                    return false;
                }
                finally
                { 
                    CloseServiceHandle(service);
                }
                return true;
            }
            finally
            {
                CloseServiceHandle(scman);
            }
        }

        /// <summary>
        /// Return the startup type of the given service
        /// </summary>
        /// <param name="serviceName">The name of the service</param> 
        /// <returns>The startup type of the given service</returns>
        /// <exception cref="Exception">The configuration couldn't be read.</exception>
        public static StartTypes GetStartupType(string serviceName)
        {
            var t = GetServiceConfig(serviceName, false);
            return t.dwStartType;
        }

        /// <summary>
        /// Change the service startup type
        /// </summary>
        /// <param name="serviceName">The name of the service</param> 
        /// <param name="startType">The new start up type</param>
        /// <returns>True if the start up type was changed successfully (or already was <paramref name="startType"/>), false on any error</returns>
        public static bool SetStartupType(string serviceName, StartTypes startType)
        {
            var t = GetServiceConfig(serviceName, true);
            if (t == null)
                return false;
            if (t.dwStartType == startType)
                return true;
            return SetServiceConfig(serviceName, ServiceTypes.SERVICE_NO_CHANGE, startType, ServiceErrors.SERVICE_NO_CHANGE, null, null, null, null, null, null, true);
        }

        /// <summary>
        /// Takes a service name, a service display name and the path to the service executable and installs (if needed) and starts the windows service (currently unused).
        /// </summary>
        /// <param name="serviceName">The service name that this service will have</param>
        /// <param name="displayName">The display name that this service will have</param>
        /// <param name="fileName">The command line of the service (executable path, quoted if needed, and arguments)</param>
        /// <exception cref="ApplicationException">The SCM couldn't be opened or the service couldn't be created.</exception>
        public static void InstallAndStart(string serviceName, string displayName, string fileName)
        {
            nint scman = OpenSCManager(ServiceManagerRights.Connect |
            ServiceManagerRights.CreateService);
            try
            {
                nint service = OpenService(scman, serviceName,
                ServiceRights.QueryStatus | ServiceRights.Start);
                if (service == nint.Zero)
                {
                    service = CreateService(scman, serviceName, displayName,
                        ServiceRights.QueryStatus | ServiceRights.Start, SERVICE_WIN32_OWN_PROCESS,
                        ServiceBootFlag.AutoStart, ServiceErrors.SERVICE_ERROR_NORMAL, fileName, null, nint.Zero,
                        null, null, null);
                }
                if (service == nint.Zero)
                {
                    var le = GetLastError();
                    throw new ApplicationException(String.Concat("Failed to install service \"", serviceName, "\", error: ", le, " (0x", le.ToString("x").PadLeft(8, '0'), ')'));
                }
                try
                {
                    StartService(service);
                }
                finally
                {
                    CloseServiceHandle(service);
                }
            }
            finally
            {
                CloseServiceHandle(scman);
            }
        }


        /// <summary>
        /// Takes a service name, a service display name and the path to the service executable and installs the windows service
        /// (own process, auto start, normal error control, running as LocalSystem).
        /// </summary>
        /// <param name="serviceName">The service name that this service will have</param>
        /// <param name="displayName">The display name that this service will have</param>
        /// <param name="fileName">The command line of the service (executable path, quoted if needed, and arguments)</param>
        /// <param name="failSilent">If true, return false on error, else throw exceptions</param>
        /// <returns>True if the service was created, false if it already existed or (with <paramref name="failSilent"/>) creation failed</returns>
        /// <exception cref="ApplicationException">The SCM couldn't be opened, or creation failed and <paramref name="failSilent"/> is false.</exception>
        public static bool Install(string serviceName, string displayName, string fileName, bool failSilent = false)
        {
            nint scman = OpenSCManager(ServiceManagerRights.Connect | ServiceManagerRights.CreateService);
            bool didInstall = false;
            try
            {
                nint service = OpenService(scman, serviceName,
                ServiceRights.QueryStatus | ServiceRights.Start);
                if (service == nint.Zero)
                {
                    service = CreateService(scman, serviceName, displayName,
                        ServiceRights.QueryStatus | ServiceRights.Start, SERVICE_WIN32_OWN_PROCESS,
                        ServiceBootFlag.AutoStart, ServiceErrors.SERVICE_ERROR_NORMAL, fileName, null, nint.Zero,
                        null, null, null);
                    didInstall = service != nint.Zero;
                }
                if (service == nint.Zero)
                {
                    var le = GetLastError();
                    if (!failSilent)
                        throw new ApplicationException(String.Concat("Failed to install service \"", serviceName, "\", error: ", le, " (0x", le.ToString("x").PadLeft(8, '0'), ')'));
                    return false;
                }
                else
                {
                    CloseServiceHandle(service);
                }
            }
            finally
            {
                CloseServiceHandle(scman);
            }
            return didInstall;
        }

        /// <summary>
        /// Takes a service name and requests it to start (doesn't wait, and a failed start request is NOT reported)
        /// </summary>
        /// <param name="serviceName">The service name</param>
        /// <exception cref="ApplicationException">The SCM or the service couldn't be opened.</exception>
        public static void Start(string serviceName)
        {
            nint scman = OpenSCManager(ServiceManagerRights.Connect);
            try
            {
                nint hService = OpenService(scman, serviceName, ServiceRights.QueryStatus | ServiceRights.Start);
                if (hService == nint.Zero)
                {
                    var le = GetLastError();
                    throw new ApplicationException(String.Concat("Could not open service \"", serviceName, "\", error: ", le, " (0x", le.ToString("x").PadLeft(8, '0'), ')'));
                }
                try
                {
                    StartService(hService);
                }
                finally
                {
                    CloseServiceHandle(hService);
                }
            }
            finally
            {
                CloseServiceHandle(scman);
            }
        }

        /// <summary>
        /// Requests the provided windows service to stop (doesn't wait, and a failed control request is NOT reported)
        /// </summary>
        /// <param name="serviceName">The service name that will be stopped</param>
        /// <exception cref="ApplicationException">The SCM or the service couldn't be opened.</exception>
        public static void Stop(string serviceName)
        {
            nint scman = OpenSCManager(ServiceManagerRights.Connect);
            try
            {
                nint hService = OpenService(scman, serviceName, ServiceRights.QueryStatus | ServiceRights.Stop);
                if (hService == nint.Zero)
                {
                    var le = GetLastError();
                    throw new ApplicationException(String.Concat("Could not open service \"", serviceName, "\", error: ", le, " (0x", le.ToString("x").PadLeft(8, '0'), ')'));
                }
                try
                {
                    StopService(hService);
                }
                finally
                {
                    CloseServiceHandle(hService);
                }
            }
            finally
            {
                CloseServiceHandle(scman);
            }
        }


        /// <summary>
        /// Requests the provided windows service to pause (doesn't wait, and a failed control request is NOT reported)
        /// </summary>
        /// <param name="serviceName">The service name that will be paused</param>
        /// <exception cref="ApplicationException">The SCM or the service couldn't be opened.</exception>
        public static void Pause(string serviceName)
        {
            nint scman = OpenSCManager(ServiceManagerRights.Connect);
            try
            {
                nint hService = OpenService(scman, serviceName, ServiceRights.QueryStatus | ServiceRights.PauseContinue);
                if (hService == nint.Zero)
                {
                    var le = GetLastError();
                    throw new ApplicationException(String.Concat("Could not open service \"", serviceName, "\", error: ", le, " (0x", le.ToString("x").PadLeft(8, '0'), ')'));
                }
                try
                {
                    PauseService(hService);
                }
                finally
                {
                    CloseServiceHandle(hService);
                }
            }
            finally
            {
                CloseServiceHandle(scman);
            }
        }

        /// <summary>
        /// Requests the provided (paused) windows service to resume (doesn't wait, and a failed control request is NOT reported)
        /// </summary>
        /// <param name="serviceName">The service name that will be resumed</param>
        /// <exception cref="ApplicationException">The SCM or the service couldn't be opened.</exception>
        public static void Continue(string serviceName)
        {
            nint scman = OpenSCManager(ServiceManagerRights.Connect);
            try
            {
                nint hService = OpenService(scman, serviceName, ServiceRights.QueryStatus | ServiceRights.PauseContinue);
                if (hService == nint.Zero)
                {
                    var le = GetLastError();
                    throw new ApplicationException(String.Concat("Could not open service \"", serviceName, "\", error: ", le, " (0x", le.ToString("x").PadLeft(8, '0'), ')'));
                }
                try
                {
                    ContinueService(hService);
                }
                finally
                {
                    CloseServiceHandle(hService);
                }
            }
            finally
            {
                CloseServiceHandle(scman);
            }
        }

        /// <summary>
        /// Takes a service name and returns the <code>ServiceState</code> of the corresponding service
        /// </summary>
        /// <param name="serviceName">The service name that we will check for his <code>ServiceState</code></param>
        /// <returns>The ServiceState of the service we wanted to check, <see cref="ServiceState.NotFound"/> if the service couldn't be opened</returns>
        /// <exception cref="ApplicationException">The SCM couldn't be opened or the status couldn't be queried.</exception>
        public static ServiceState GetServiceStatus(string serviceName)
        {
            nint scman = OpenSCManager(ServiceManagerRights.Connect);
            try
            {
                nint hService = OpenService(scman, serviceName,
                ServiceRights.QueryStatus);
                if (hService == nint.Zero)
                {
                    return ServiceState.NotFound;
                }
                try
                {
                    return GetServiceStatus(hService);
                }
                finally
                {
                    CloseServiceHandle(hService);
                }
            }
            finally
            {
                CloseServiceHandle(scman);
            }
        }

        #region Internal

        /// <summary>
        /// Throws an exception with the last Win32 error code, call after an API call reported failure (through its return value).
        /// </summary>
        /// <typeparam name="T">The return type (to allow "return ThrowNative&lt;T&gt;()").</typeparam>
        /// <returns>Never returns.</returns>
        /// <exception cref="Exception">Always.</exception>
        static T ThrowNative<T>()
        {
            var r = GetLastError();
            throw new Exception("Win32 failure: " + r + " (0x" + r.ToString("x").PadLeft(8, '0') + ')');
        }

        /// <summary>
        /// Change the service configuration using ChangeServiceConfig, use the NO_CHANGE values / null for settings that should not change.
        /// </summary>
        /// <returns>The result of ChangeServiceConfig, false on exceptions if <paramref name="failSilent"/>.</returns>
        static bool SetServiceConfig(string serviceName, ServiceTypes dwServiceType, StartTypes dwStartType, ServiceErrors dwErrorControl, string lpBinaryPathName, string lpLoadOrderGroup, string lpDependencies, string lpServiceStartName, string lpPassword, string lpDisplayName, bool failSilent = true)
        {
            var manager = OpenSCManager(null, null, ServiceManagerRights.GENERIC_READ);
            try
            {
                var service = OpenService(manager, serviceName, ServiceRights.ChangeConfig);
                try
                {

                    return ChangeServiceConfig(service, dwServiceType, dwStartType, dwErrorControl, lpBinaryPathName, lpLoadOrderGroup, out var x, lpDependencies, lpServiceStartName, lpPassword, lpDisplayName);
                }
                finally
                {
                    CloseServiceHandle(service);
                }
            }
            catch
            {
                if (failSilent)
                    return false;
                throw;
            }
            finally
            {
                CloseServiceHandle(manager);
            }
        }


        /// <summary>
        /// Query optional configuration (QueryServiceConfig2) into a marshalled class.
        /// </summary>
        /// <returns>The configuration, null on errors if <paramref name="failSilent"/> is true.</returns>
        static T GetServiceConfig2<T>(IntPtr service, uint infoLevel, bool failSilent = true) where T : class, new()
        {
            try
            {
                if (!QueryServiceConfig2(service, infoLevel, nint.Zero, 0, out var bytesNeeded))
                {
                    if (GetLastError() != 122)
                        return ThrowNative<T>();
                }
                nint ptr = Marshal.AllocHGlobal((int)bytesNeeded);
                try
                {
                    if (!QueryServiceConfig2(service, infoLevel, ptr, bytesNeeded, out bytesNeeded))
                        return ThrowNative<T>();
                    T descriptionStruct = new T();
                    Marshal.PtrToStructure(ptr, descriptionStruct);
                    return descriptionStruct;
                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                }
            }
            catch
            {
                if (failSilent)
                    return null;
                throw;
            }
        }

        static T GetServiceConfig2<T>(string serviceName, uint infoLevel, bool failSilent = true) where T : class, new()
        {
            var manager = OpenSCManager(null, null, ServiceManagerRights.GENERIC_READ);
            try
            {
                var service = OpenService(manager, serviceName, ServiceRights.QueryConfig);
                try
                {
                    return GetServiceConfig2<T>(service, infoLevel, failSilent);
                }
                finally
                {
                    CloseServiceHandle(service);
                }
            }
            catch
            {
                if (failSilent)
                    return null;
                throw;
            }
            finally
            {
                CloseServiceHandle(manager);
            }
        }

        /// <summary>
        /// Set optional configuration (ChangeServiceConfig2) from a marshalled class, success is determined using the return value of ChangeServiceConfig2.
        /// </summary>
        static bool SetServiceConfig2<T>(IntPtr service, uint infoLevel, T value, bool failSilent = false)
        {
            var bytesNeeded = Marshal.SizeOf(value);
            nint ptr = Marshal.AllocHGlobal(bytesNeeded);
            try
            {
                Marshal.StructureToPtr(value, ptr, false);
                if (!ChangeServiceConfig2(service, infoLevel, ptr))
                    return ThrowNative<bool>();
                return true;
            }
            catch
            {
                if (failSilent)
                    return false;
                throw;
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }


        static bool SetServiceConfig2<T>(string serviceName, uint infoLevel, T value, bool failSilent = false)
        {
            try
            {
                var manager = OpenSCManager(null, null, ServiceManagerRights.GENERIC_WRITE);
                try
                {
                    var service = OpenService(manager, serviceName, ServiceRights.ChangeConfig | ServiceRights.Start);
                    try
                    {
                        return SetServiceConfig2(service, infoLevel, value, failSilent);
                    }
                    finally
                    {
                        CloseServiceHandle(service);
                    }
                }
                finally
                {
                    CloseServiceHandle(manager);
                }
            }
            catch
            {
                if (failSilent)
                    return false;
                throw;
            }
        }

        /// <summary>
        /// Query extended service status (QueryServiceStatusEx) into a marshalled class (currently unused).
        /// </summary>
        static T GetServiceStatus<T>(string serviceName, uint infoLevel, bool failSilent = true) where T : class, new()
        {
            var manager = OpenSCManager(null, null, ServiceManagerRights.GENERIC_READ);
            try
            {
                var service = OpenService(manager, serviceName, ServiceRights.QueryStatus);
                try
                {
                    if (!QueryServiceStatusEx(service, infoLevel, nint.Zero, 0, out var bytesNeeded))
                    {
                        if (GetLastError() != 122)
                            return ThrowNative<T>();
                    }
                    nint ptr = Marshal.AllocHGlobal(bytesNeeded);
                    try
                    {
                        if (!QueryServiceStatusEx(service, infoLevel, ptr, bytesNeeded, out bytesNeeded))
                            return ThrowNative<T>();
                        T descriptionStruct = new T();
                        Marshal.PtrToStructure(ptr, descriptionStruct);
                        return descriptionStruct;
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(ptr);
                    }
                }
                finally
                {
                    CloseServiceHandle(service);
                }
            }
            catch
            {
                if (failSilent)
                    return null;
                throw;
            }
            finally
            {
                CloseServiceHandle(manager);
            }
        }


        static string GetDescription(IntPtr service, bool failSilent = false)
        {
            var t = GetServiceConfig2<ServiceDescription>(service, SERVICE_CONFIG_DESCRIPTION, failSilent);
            return t?.lpDescription;
        }

        static bool SetDescription(IntPtr service, string description, bool failSilent = false)
        {
            var t = new ServiceDescription
            {
                lpDescription = description
            };
            return SetServiceConfig2(service, SERVICE_CONFIG_DESCRIPTION, t, failSilent);
        }

        static QueryServiceConfig GetServiceConfig(string serviceName, bool failSilent = true)
        {
            var manager = OpenSCManager(null, null, ServiceManagerRights.GENERIC_READ);
            try
            {
                var service = OpenService(manager, serviceName, ServiceRights.QueryConfig);
                try
                {
                    if (!QueryServiceConfig(service, nint.Zero, 0, out var bytesNeeded))
                    {
                        if (GetLastError() != 122)
                            return ThrowNative<QueryServiceConfig>();
                    }
                    nint ptr = Marshal.AllocHGlobal((int)bytesNeeded);
                    try
                    {
                        if (!QueryServiceConfig(service, ptr, bytesNeeded, out bytesNeeded))
                            return ThrowNative<QueryServiceConfig>();
                        QueryServiceConfig descriptionStruct = new QueryServiceConfig();
                        Marshal.PtrToStructure(ptr, descriptionStruct);
                        return descriptionStruct;
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(ptr);
                    }
                }
                finally
                {
                    CloseServiceHandle(service);
                }
            }
            catch
            {
                if (failSilent)
                    return null;
                throw;
            }
            finally
            {
                CloseServiceHandle(manager);
            }
        }

        /// <summary>
        /// Starts the provided windows service (the result is ignored)
        /// </summary>
        /// <param name="hService">The handle to the windows service</param>
        static void StartService(nint hService)
        {
            ServiceStatus status = new ServiceStatus();
            StartService(hService, 0, 0);
        }

        /// <summary>
        /// Stops the provided windows service
        /// </summary>
        /// <param name="hService">The handle to the windows service</param>
        static void StopService(nint hService)
        {
            ServiceStatus status = new ServiceStatus();
            ControlService(hService, ServiceControl.Stop, status);
        }

        /// <summary>
        /// Pause the provided windows service
        /// </summary>
        /// <param name="hService">The handle to the windows service</param>
        static void PauseService(nint hService)
        {
            ServiceStatus status = new ServiceStatus();
            ControlService(hService, ServiceControl.Pause, status);
        }

        /// <summary>
        /// Resume the provided windows service after a pause
        /// </summary>
        /// <param name="hService">The handle to the windows service</param>
        static void ContinueService(nint hService)
        {
            ServiceStatus status = new ServiceStatus();
            ControlService(hService, ServiceControl.Continue, status);
        }


        /// <summary>
        /// Gets the service state by using the handle of the provided windows service
        /// </summary>
        /// <param name="hService">The handle to the service</param>
        /// <returns>The <code>ServiceState</code> of the service</returns>
        static ServiceState GetServiceStatus(nint hService)
        {
            ServiceStatus ssStatus = new ServiceStatus();
            if (QueryServiceStatus(hService, ssStatus) == 0)
            {
                throw new ApplicationException("Failed to query service status.");
            }
            return ssStatus.dwCurrentState;
        }

        /// <summary>
        /// Returns true when the service status has been changed from wait status to desired status (currently unused).
        /// Waits while the service makes progress (check point increases), giving up if no progress is made within the wait hint.
        /// </summary>
        /// <param name="hService">The handle to the service</param>
        /// <param name="WaitStatus">The current state of the service</param>
        /// <param name="DesiredStatus">The desired state of the service</param>
        /// <returns>bool if the service has successfully changed states within the allowed timeline</returns>
        static bool WaitForServiceStatus(nint hService, ServiceState
        WaitStatus, ServiceState DesiredStatus)
        {
            ServiceStatus ssStatus = new ServiceStatus();
            int dwOldCheckPoint;
            int dwStartTickCount;

            QueryServiceStatus(hService, ssStatus);
            if (ssStatus.dwCurrentState == DesiredStatus) return true;
            dwStartTickCount = Environment.TickCount;
            dwOldCheckPoint = ssStatus.dwCheckPoint;

            while (ssStatus.dwCurrentState == WaitStatus)
            {
                // Do not wait longer than the wait hint. A good interval is
                // one tenth the wait hint, but no less than 1 second and no
                // more than 10 seconds.

                int dwWaitTime = ssStatus.dwWaitHint / 10;

                if (dwWaitTime < 1000) dwWaitTime = 1000;
                else if (dwWaitTime > 10000) dwWaitTime = 10000;

                System.Threading.Thread.Sleep(dwWaitTime);

                // Check the status again.

                if (QueryServiceStatus(hService, ssStatus) == 0) break;

                if (ssStatus.dwCheckPoint > dwOldCheckPoint)
                {
                    // The service is making progress.
                    dwStartTickCount = Environment.TickCount;
                    dwOldCheckPoint = ssStatus.dwCheckPoint;
                }
                else
                {
                    if (Environment.TickCount - dwStartTickCount > ssStatus.dwWaitHint)
                    {
                        // No progress made within the wait hint
                        break;
                    }
                }
            }
            return ssStatus.dwCurrentState == DesiredStatus;
        }

        /// <summary>
        /// Opens the service manager
        /// </summary>
        /// <param name="Rights">The service manager rights</param>
        /// <returns>the handle to the service manager, must be closed using CloseServiceHandle</returns>
        /// <exception cref="ApplicationException">The SCM couldn't be opened.</exception>
        static nint OpenSCManager(ServiceManagerRights Rights)
        {
            nint scman = OpenSCManager(null, null, Rights);
            if (scman == nint.Zero)
            {
                throw new ApplicationException("Could not connect to service control manager.");
            }
            return scman;
        }


        #endregion//Internal



        #region Interop

        const int STANDARD_RIGHTS_REQUIRED = 0xF0000;
        const int SERVICE_WIN32_OWN_PROCESS = 0x00000010;

        [DllImport("advapi32.dll", EntryPoint = "OpenSCManagerA", SetLastError = true)]
        static extern nint OpenSCManager(string lpMachineName, string lpDatabaseName, ServiceManagerRights dwDesiredAccess);

        [DllImport("advapi32.dll", EntryPoint = "OpenServiceA", CharSet = CharSet.Ansi, SetLastError = true)]
        static extern nint OpenService(nint hSCManager, string lpServiceName, ServiceRights dwDesiredAccess);

        [DllImport("advapi32.dll", EntryPoint = "CreateServiceA", SetLastError = true)]
        static extern nint CreateService(nint hSCManager, string lpServiceName, string lpDisplayName, ServiceRights dwDesiredAccess, int dwServiceType, ServiceBootFlag dwStartType, ServiceErrors dwErrorControl, string lpBinaryPathName, string lpLoadOrderGroup, nint lpdwTagId, string lpDependencies, string lp, string lpPassword);

        [DllImport("advapi32.dll")]
        static extern int CloseServiceHandle(nint hSCObject);

        [DllImport("advapi32.dll")]
        static extern int QueryServiceStatus(nint hService, ServiceStatus lpServiceStatus);

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern int DeleteService(nint hService);

        [DllImport("advapi32.dll")]
        static extern int ControlService(nint hService, ServiceControl dwControl, ServiceStatus lpServiceStatus);

        [DllImport("advapi32.dll", EntryPoint = "StartServiceA")]
        static extern int StartService(nint hService, int dwNumServiceArgs, int lpServiceArgVectors);

        [DllImport("advapi32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool ControlService(nint hService, ServiceControl dwControl, ref ServiceStatus lpServiceStatus);

        const uint SERVICE_CONFIG_DESCRIPTION = 0x01;
        const uint SERVICE_CONFIG_FAILURE_ACTIONS = 0x02;
        const uint SERVICE_CONFIG_DELAYED_AUTO_START_INFO = 3;

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool QueryServiceStatusEx(nint serviceHandle, uint infoLevel, nint buffer, int bufferSize, out int bytesNeeded);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "QueryServiceConfigW")]
        static extern bool QueryServiceConfig(nint hService, nint buffer, uint cbBufSize, out uint pcbBytesNeeded);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "QueryServiceConfig2W")]
        static extern bool QueryServiceConfig2(nint hService, uint dwInfoLevel, nint buffer, uint cbBufSize, out uint pcbBytesNeeded);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "ChangeServiceConfigW")]
        static extern bool ChangeServiceConfig(nint hService, ServiceTypes dwServiceType, StartTypes dwStartType, ServiceErrors dwErrorControl, string lpBinaryPathName,
            string lpLoadOrderGroup, out uint lpdwTagId, string lpDependencies, string lpServiceStartName, string lpPassword, string lpDisplayName);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "ChangeServiceConfig2W")]
        static extern bool ChangeServiceConfig2(nint hService, uint dwInfoLevel, nint lpInfo);

        /// <summary>
        /// The Win32 error code of the last P/Invoke call declared with SetLastError = true (calling kernel32 GetLastError directly is unreliable, the runtime may overwrite it).
        /// </summary>
        static uint GetLastError() => (uint)Marshal.GetLastWin32Error();

        #endregion//Interop

    }

}
