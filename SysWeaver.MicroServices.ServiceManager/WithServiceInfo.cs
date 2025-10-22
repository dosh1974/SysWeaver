using System;
using SysWeaver.MicroService;

namespace SysWeaver
{
    public abstract class WithServiceInfo
    {
        protected WithServiceInfo(ServiceManager manager, String defaultInstanceName = "default")
        {
            Manager = manager;
            DefInstanceName = String.IsNullOrEmpty(defaultInstanceName) ? "default" : defaultInstanceName;

        }
        readonly String DefInstanceName;

        protected readonly ServiceManager Manager;

        /// <summary>
        /// Get the name of the instance (if created by the manager).
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
