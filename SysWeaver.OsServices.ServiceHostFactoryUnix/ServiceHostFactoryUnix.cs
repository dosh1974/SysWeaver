using System;

namespace SysWeaver.OsServices
{
    public class ServiceHostFactoryUnix : IServiceHostFactory
    {
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
