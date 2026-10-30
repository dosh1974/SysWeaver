namespace SysWeaver.OsServices
{
    /// <summary>
    /// Creates the OS specific <see cref="IServiceHost"/>.
    /// <see cref="ServiceHost.Run"/> looks for an implementation named "SysWeaver.OsServices.ServiceHostFactory[Platform]" (where Platform is <see cref="System.Environment.OSVersion"/>.Platform, ex: Win32NT or Unix),
    /// in loaded assemblies or in an assembly file with the same name next to the executable. Implementations must have a public parameterless constructor.
    /// </summary>
    public interface IServiceHostFactory
    {
        /// <summary>
        /// Create the service host for the current OS.
        /// </summary>
        /// <param name="p">The service parameters (name etc is already resolved).</param>
        /// <returns>The service host for this OS.</returns>
        IServiceHost Create(ServiceParams p);

        /// <summary>
        /// Create an object that lists and controls all services of the OS (used by <see cref="OsSystem.Services"/>).
        /// </summary>
        /// <returns>The system services or null if not supported on this OS</returns>
        ISystemServices CreateSystemServices() => null;

        /// <summary>
        /// Create an object that lists and edits the rules of the OS firewall (used by <see cref="OsSystem.Firewall"/>).
        /// </summary>
        /// <returns>The firewall or null if not supported on this OS (or no supported firewall is installed)</returns>
        IFirewall CreateFirewall() => null;

        /// <summary>
        /// Create an object that lists the disks and volumes (used by <see cref="OsSystem.Storage"/>).
        /// </summary>
        /// <returns>The storage or null if not supported on this OS</returns>
        IStorage CreateStorage() => null;

        /// <summary>
        /// Create an object that reads and changes the network configuration (used by <see cref="OsSystem.NetworkConfig"/>).
        /// </summary>
        /// <returns>The network configuration or null if not supported on this OS</returns>
        INetworkConfig CreateNetworkConfig() => null;

        /// <summary>
        /// Create an object that reads and changes environment variables (used by <see cref="OsSystem.Environment"/>).
        /// </summary>
        /// <returns>The environment variables or null if not supported on this OS</returns>
        IEnvironmentVariables CreateEnvironment() => null;

        /// <summary>
        /// Create an object that lists and changes the startup items (used by <see cref="OsSystem.Startup"/>).
        /// </summary>
        /// <returns>The startup items or null if not supported on this OS</returns>
        IStartupItems CreateStartupItems() => null;

        /// <summary>
        /// Create an object that shuts down, restarts or suspends the system (used by <see cref="OsSystem.Power"/>).
        /// </summary>
        /// <returns>The power control or null if not supported on this OS</returns>
        IPower CreatePower() => null;

        /// <summary>
        /// Create an object that reads and changes file permissions (used by <see cref="OsSystem.FilePermissions"/>).
        /// </summary>
        /// <returns>The file permissions or null if not supported on this OS</returns>
        IFilePermissions CreateFilePermissions() => null;
    }

}
