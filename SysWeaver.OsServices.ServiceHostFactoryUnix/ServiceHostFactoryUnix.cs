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
        /// <returns>A systemd or SysVinit service host, or null (after writing a message to the console) if the init system isn't supported.</returns>
        public IServiceHost Create(ServiceParams p)
        {
            if (SystemHelper.Run("systemd-notify --booted") == 0)
                return new ServiceHostSystemD(p);
            var serviceSystem = SystemHelper.GetStdOutFrom("ps -p 1 -o comm=").Trim();
            if (serviceSystem == "systemd")
                return new ServiceHostSystemD(p);
            if (serviceSystem == "init")
                return new ServiceHostSysVinit(p);
            Console.WriteLine("Service system " + serviceSystem.ToQuoted() + " is not known!");
            return null;
        }
    }
}
