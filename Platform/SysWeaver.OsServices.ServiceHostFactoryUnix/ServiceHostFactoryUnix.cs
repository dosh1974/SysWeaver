using System;

namespace SysWeaver.OsServices
{
    /// <summary>
    /// <see cref="IServiceHostFactory"/> for Unix (Linux), found by name by <see cref="ServiceHost.Run"/>.
    /// Detects the init system: systemd (if "systemd-notify --booted" succeeds or PID 1 is "systemd") or SysVinit (PID 1 is "init").
    /// </summary>
    public class ServiceHostFactoryUnix : IServiceHostFactory
    {
        /// <summary>
        /// Create a service host for the detected init system.
        /// </summary>
        /// <param name="p">The service parameters.</param>
        /// <returns>A systemd or SysVinit service host, or null (after writing a message to the console) if the init system isn't supported (or "ps" isn't available).</returns>
        public IServiceHost Create(ServiceParams p)
        {
            if (SystemHelper.Run("systemd-notify --booted") == 0)
                return new ServiceHostSystemD(p);
            var serviceSystem = SystemHelper.GetStdOutFrom("ps -p 1 -o comm=")?.Trim() ?? "";
            if (serviceSystem == "systemd")
                return new ServiceHostSystemD(p);
            if (serviceSystem == "init")
                return new ServiceHostSysVinit(p);
            Console.WriteLine("Service system " + serviceSystem.ToQuoted() + " is not known!");
            return null;
        }

        /// <summary>
        /// Create an object that lists and controls all services, systemd (if "/run/systemd/system" exists, like sd_booted) or SysVinit (if "/etc/init.d" exists).
        /// </summary>
        /// <returns>The system services or null if no supported init system was found</returns>
        public ISystemServices CreateSystemServices()
        {
            if (System.IO.Directory.Exists("/run/systemd/system") && (UnixCommand.Find("systemctl") != null))
                return new SystemdSystemServices();
            if (System.IO.Directory.Exists("/etc/init.d"))
                return new SysVinitSystemServices();
            return null;
        }

        /// <summary>
        /// Create an object that lists and edits the firewall rules, ufw (if installed) or iptables (if installed).
        /// </summary>
        /// <returns>The firewall or null if no supported firewall was found</returns>
        public IFirewall CreateFirewall()
        {
            var ufw = UnixCommand.Find("ufw");
            if (ufw != null)
                return new UfwFirewall(ufw);
            var ipt = UnixCommand.Find("iptables");
            if (ipt != null)
                return new IptablesFirewall(ipt, UnixCommand.Find("ip6tables"));
            return null;
        }

        /// <summary>
        /// Create an object that lists the disks and volumes (lsblk and the mounted file systems).
        /// </summary>
        /// <returns>The storage or null if lsblk isn't installed</returns>
        public IStorage CreateStorage()
        {
            var lsblk = UnixCommand.Find("lsblk");
            return lsblk != null ? new UnixStorage(lsblk) : null;
        }

        /// <summary>
        /// Create an object that reads and changes the network configuration (uses "ip" from iproute2 for changes and routes).
        /// </summary>
        public INetworkConfig CreateNetworkConfig() => new UnixNetworkConfig(UnixCommand.Find("ip"));

        /// <summary>
        /// Create an object that reads and changes the process, user (~/.config/environment.d) and machine (/etc/environment) environment variables.
        /// </summary>
        public IEnvironmentVariables CreateEnvironment() => new UnixEnvironment();

        /// <summary>
        /// Create an object that lists and changes the XDG autostart items, cron @reboot jobs and rc.local.
        /// </summary>
        public IStartupItems CreateStartupItems() => new UnixStartup();

        /// <summary>
        /// Create an object that shuts down, restarts or suspends the system ("shutdown" and "systemctl").
        /// </summary>
        /// <returns>The power control or null if neither command is installed</returns>
        public IPower CreatePower()
        {
            var shutdown = UnixCommand.Find("shutdown");
            var systemctl = UnixCommand.Find("systemctl");
            return (shutdown != null) || (systemctl != null) ? new UnixPower(shutdown, systemctl) : null;
        }

        /// <summary>
        /// Create an object that reads and changes the owner, group and mode of files.
        /// </summary>
        public IFilePermissions CreateFilePermissions() => new UnixFilePermissions();
    }
}
