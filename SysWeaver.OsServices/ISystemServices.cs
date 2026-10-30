using System;
using System.Collections.Generic;

namespace SysWeaver.OsServices
{
    /// <summary>
    /// Lists and controls all services of the OS service system (Windows Service Control Manager, systemd or SysVinit).
    /// Get an instance using <see cref="OsSystem.Services"/> (created by <see cref="IServiceHostFactory.CreateSystemServices"/>).
    /// Most control operations requires elevation (admin / root), failures are reported by throwing an exception (with a description).
    /// </summary>
    public interface ISystemServices
    {
        /// <summary>
        /// The display name of the service system, ex: "Windows Service Control Manager" or "Linux systemd".
        /// </summary>
        String Name { get; }

        /// <summary>
        /// The start modes that can be used with <see cref="SetStartMode"/>.
        /// </summary>
        IReadOnlyList<OsServiceStartModes> StartModes { get; }

        /// <summary>
        /// Get all services (the <see cref="OsServiceInfo.Dependencies"/>, <see cref="OsServiceInfo.Dependents"/> and <see cref="OsServiceInfo.Properties"/> are not filled).
        /// </summary>
        /// <returns>The services</returns>
        IReadOnlyList<OsServiceInfo> GetServices();

        /// <summary>
        /// Get all details about a service.
        /// </summary>
        /// <param name="name">The name of the service</param>
        /// <returns>The details or null if the service doesn't exist</returns>
        OsServiceInfo GetService(String name);

        /// <summary>
        /// Start a service (waits for a while for it to start).
        /// </summary>
        /// <param name="name">The name of the service</param>
        void Start(String name);

        /// <summary>
        /// Stop a service (and services that depends on it), waits for a while for it to stop.
        /// </summary>
        /// <param name="name">The name of the service</param>
        void Stop(String name);

        /// <summary>
        /// Stop and start a service.
        /// </summary>
        /// <param name="name">The name of the service</param>
        void Restart(String name);

        /// <summary>
        /// Pause a service.
        /// </summary>
        /// <param name="name">The name of the service</param>
        void Pause(String name);

        /// <summary>
        /// Resume a paused service.
        /// </summary>
        /// <param name="name">The name of the service</param>
        void Continue(String name);

        /// <summary>
        /// Ask the service to reload it's configuration (if supported).
        /// </summary>
        /// <param name="name">The name of the service</param>
        void Reload(String name);

        /// <summary>
        /// Change how a service is started.
        /// </summary>
        /// <param name="name">The name of the service</param>
        /// <param name="mode">The new start mode, must be one of <see cref="StartModes"/></param>
        void SetStartMode(String name, OsServiceStartModes mode);

        /// <summary>
        /// Stop and delete (uninstall) a service.
        /// </summary>
        /// <param name="name">The name of the service</param>
        void Delete(String name);
    }
}
