using System;
using System.IO;
using System.Reflection;

namespace SysWeaver.OsServices
{
    /// <summary>
    /// Access to OS specific functionality (all services of the OS and the firewall), implemented by the platform factory assemblies
    /// (SysWeaver.OsServices.ServiceHostFactory[Platform], the same <see cref="IServiceHostFactory"/> that <see cref="ServiceHost.Run"/> uses).
    /// The factory is found in loaded assemblies or in an assembly file with the same name next to the executable.
    /// </summary>
    public static class OsSystem
    {
        /// <summary>
        /// The OS specific factory, null if it can't be found (see <see cref="FactoryError"/>).
        /// </summary>
        public static IServiceHostFactory Factory => IntFactory.Value.Factory;

        /// <summary>
        /// Why the factory couldn't be found (null if it was found).
        /// </summary>
        public static String FactoryError => IntFactory.Value.Error;

        /// <summary>
        /// Lists and controls all services of the OS, null if not supported.
        /// </summary>
        public static ISystemServices Services => IntServices.Value;

        /// <summary>
        /// Lists and edits the firewall rules, null if not supported (or no supported firewall is installed).
        /// </summary>
        public static IFirewall Firewall => IntFirewall.Value;

        /// <summary>
        /// Get the system services or throw an exception describing why it's not available.
        /// </summary>
        /// <returns>The system services</returns>
        public static ISystemServices RequireServices()
            => Services ?? throw new PlatformNotSupportedException(FactoryError ?? ("Listing services is not supported on " + System.Environment.OSVersion.Platform + "!"));

        /// <summary>
        /// Get the firewall or throw an exception describing why it's not available.
        /// </summary>
        /// <returns>The firewall</returns>
        public static IFirewall RequireFirewall()
            => Firewall ?? throw new PlatformNotSupportedException(FactoryError ?? "No supported firewall was found!");

        /// <summary>
        /// Lists the disks and volumes, null if not supported.
        /// </summary>
        public static IStorage Storage => IntStorage.Value;

        /// <summary>
        /// Reads and changes the network configuration (adapters, routes, DNS cache), null if not supported.
        /// </summary>
        public static INetworkConfig NetworkConfig => IntNetworkConfig.Value;

        /// <summary>
        /// Reads and changes environment variables (process, user and machine), null if not supported.
        /// </summary>
        public static IEnvironmentVariables Environment => IntEnvironment.Value;

        /// <summary>
        /// Lists and changes the programs started when the system starts or a user logs on, null if not supported.
        /// </summary>
        public static IStartupItems Startup => IntStartup.Value;

        /// <summary>
        /// Shuts down, restarts or suspends the system, null if not supported.
        /// </summary>
        public static IPower Power => IntPower.Value;

        /// <summary>
        /// Reads and changes file permissions, null if not supported.
        /// </summary>
        public static IFilePermissions FilePermissions => IntFilePermissions.Value;

        static T Require<T>(T value, String what) where T : class
            => value ?? throw new PlatformNotSupportedException(FactoryError ?? (what + " is not supported on " + System.Environment.OSVersion.Platform + "!"));

        /// <summary>
        /// Get the storage or throw an exception describing why it's not available.
        /// </summary>
        public static IStorage RequireStorage() => Require(Storage, "Listing disks");

        /// <summary>
        /// Get the network configuration or throw an exception describing why it's not available.
        /// </summary>
        public static INetworkConfig RequireNetworkConfig() => Require(NetworkConfig, "The network configuration");

        /// <summary>
        /// Get the environment variables or throw an exception describing why it's not available.
        /// </summary>
        public static IEnvironmentVariables RequireEnvironment() => Require(Environment, "Environment variables");

        /// <summary>
        /// Get the startup items or throw an exception describing why it's not available.
        /// </summary>
        public static IStartupItems RequireStartup() => Require(Startup, "Startup items");

        /// <summary>
        /// Get the power control or throw an exception describing why it's not available.
        /// </summary>
        public static IPower RequirePower() => Require(Power, "Power control");

        /// <summary>
        /// Get the file permissions or throw an exception describing why it's not available.
        /// </summary>
        public static IFilePermissions RequireFilePermissions() => Require(FilePermissions, "File permissions");

        static readonly Lazy<(IServiceHostFactory Factory, String Error)> IntFactory = new(FindFactory);
        static readonly Lazy<ISystemServices> IntServices = new(() => Factory?.CreateSystemServices());
        static readonly Lazy<IFirewall> IntFirewall = new(() => Factory?.CreateFirewall());
        static readonly Lazy<IStorage> IntStorage = new(() => Factory?.CreateStorage());
        static readonly Lazy<INetworkConfig> IntNetworkConfig = new(() => Factory?.CreateNetworkConfig());
        static readonly Lazy<IEnvironmentVariables> IntEnvironment = new(() => Factory?.CreateEnvironment());
        static readonly Lazy<IStartupItems> IntStartup = new(() => Factory?.CreateStartupItems());
        static readonly Lazy<IPower> IntPower = new(() => Factory?.CreatePower());
        static readonly Lazy<IFilePermissions> IntFilePermissions = new(() => Factory?.CreateFilePermissions());

        static (IServiceHostFactory, String) FindFactory()
        {
            String dname = String.Concat(typeof(ServiceHost).Namespace, '.', nameof(ServiceHost), "Factory", System.Environment.OSVersion.Platform);
            try
            {
                var dtype = TypeFinder.Get(dname) ?? TypeFinder.Get(String.Concat(dname, ", ", dname));
                if (dtype == null)
                {
                    var dllName = Path.Combine(EnvInfo.ExecutableDir, dname + ".dll");
                    if (File.Exists(dllName))
                        dtype = Assembly.LoadFile(dllName).GetType(dname);
                }
                if (dtype == null)
                    return (null, "The type \"" + dname + "\" can't be found!");
                if (!typeof(IServiceHostFactory).IsAssignableFrom(dtype))
                    return (null, "The type \"" + dtype.FullName + "\" doesn't implement \"" + typeof(IServiceHostFactory).FullName + "\"!");
                return (Activator.CreateInstance(dtype) as IServiceHostFactory, null);
            }
            catch (Exception ex)
            {
                return (null, "Failed to create \"" + dname + "\": " + ex.Message);
            }
        }
    }
}
