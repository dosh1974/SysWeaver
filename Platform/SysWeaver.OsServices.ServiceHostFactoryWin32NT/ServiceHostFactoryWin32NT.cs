namespace SysWeaver.OsServices
{
    /// <summary>
    /// <see cref="IServiceHostFactory"/> for Windows (Win32NT), found by name by <see cref="ServiceHost.Run"/>.
    /// </summary>
    public sealed class ServiceHostFactoryWin32NT : IServiceHostFactory
    {
        /// <summary>
        /// Create a host that uses the Windows Service Control Manager.
        /// </summary>
        /// <param name="p">The service parameters.</param>
        /// <returns>The service host.</returns>
        public IServiceHost Create(ServiceParams p) => new ServiceHostWindows(p);

        /// <summary>
        /// Create an object that lists and controls all Windows services (using the Service Control Manager).
        /// </summary>
        /// <returns>The system services</returns>
        public ISystemServices CreateSystemServices() => new Win32SystemServices();

        /// <summary>
        /// Create an object that lists and edits the Windows Defender Firewall rules.
        /// </summary>
        /// <returns>The firewall</returns>
        public IFirewall CreateFirewall() => new Win32Firewall();

        /// <summary>
        /// Create an object that lists the disks and volumes (Storage Management WMI, BitLocker).
        /// </summary>
        public IStorage CreateStorage() => new Win32Storage();

        /// <summary>
        /// Create an object that reads and changes the network configuration (adapters, routes, DNS cache).
        /// </summary>
        public INetworkConfig CreateNetworkConfig() => new Win32NetworkConfig();

        /// <summary>
        /// Create an object that reads and changes the process, user (HKCU) and machine (HKLM) environment variables.
        /// </summary>
        public IEnvironmentVariables CreateEnvironment() => new Win32Environment();

        /// <summary>
        /// Create an object that lists and changes the Run keys and the startup folders.
        /// </summary>
        public IStartupItems CreateStartupItems() => new Win32Startup();

        /// <summary>
        /// Create an object that shuts down, restarts, suspends or hibernates the system.
        /// </summary>
        public IPower CreatePower() => new Win32Power();

        /// <summary>
        /// Create an object that reads and changes NTFS permissions (ACL's).
        /// </summary>
        public IFilePermissions CreateFilePermissions() => new Win32FilePermissions();
    }
}
