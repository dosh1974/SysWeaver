using System;
using SysWeaver.MicroService;

namespace SysWeaver
{
    /// <summary>
    /// Base class for services that want to know their instance name and service info
    /// </summary>
    public abstract class WithServiceInfo
    {
        /// <summary>
        /// Create a service base object
        /// </summary>
        /// <param name="manager">The service manager that the service is registered to</param>
        /// <param name="defaultInstanceName">The instance name to use if the manager has no name for the instance, null or empty means "default"</param>
        protected WithServiceInfo(ServiceManager manager, String defaultInstanceName = "default")
        {
            Manager = manager;
            DefInstanceName = String.IsNullOrEmpty(defaultInstanceName) ? "default" : defaultInstanceName;

        }
        readonly String DefInstanceName;

        /// <summary>
        /// The service manager
        /// </summary>
        protected readonly ServiceManager Manager;

        /// <summary>
        /// Get the name of the instance (if created by the manager), the default instance name if it has no name.
        /// Can't be called in the constructor.
        /// </summary>
        public String InstanceName
        {
            get
            {
                var t = InternalInstanceName;
                if (t != null)
                    return t;
                var i = Manager.GetInfo(this);
                InternalServiceInfo = i;
                t = i.Name;
                if (String.IsNullOrEmpty(t))
                    t = DefInstanceName;
                InternalInstanceName = t;
                return t;
            }
        }
        String InternalInstanceName;
        ServiceInfo InternalServiceInfo;

        /// <summary>
        /// Get service instance information (if created by the manager).
        /// Can't be called in the constructor.
        /// </summary>
        public ServiceInfo ServiceInfo
        {
            get
            {
                var i = InternalServiceInfo;
                if (i != null)
                    return i;
                i = Manager.GetInfo(this);
                InternalServiceInfo = i;
                var t = i.Name;
                if (String.IsNullOrEmpty(t))
                    t = DefInstanceName;
                InternalInstanceName = t;
                return i;
            }
        }


    }


}
