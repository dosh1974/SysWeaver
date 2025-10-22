using SysWeaver.Net;
using System;

namespace SysWeaver.MicroService
{



    [IsMicroService]
    public sealed class ApiHttpServerService : IDisposable
    {
        public override string ToString() => "[Service] " + Mod.ToString();

        public ApiHttpServerService(ServiceManager manager, ApiHttpServerServiceParams p = null)
        {
            Manager = manager;
            p = p ?? new ApiHttpServerServiceParams();
            var mod = new ApiHttpServerModule(p);
            Mod = mod;
            using (manager.Tab())
            {
                if (mod.AddObject(manager))
                    manager.AddMessage(MsgPrefix + "Added API methods from type \"" + manager.GetType() + "\" (" + manager + ")", MessageLevels.Debug);
                using (manager.Tab())
                {
                    foreach (var o in manager.GetAll(ServiceInstanceTypes.LocalOnly))
                    {
                        if (mod.AddObject(o))
                            manager.AddMessage(MsgPrefix + "Added API methods from type \"" + o.GetType() + "\" (" + o + ")", MessageLevels.Debug);
                    }
                }
            }
            //  Handle future add/removes
            foreach (var x in manager.GetAll<IApiAuditService>())
            {
                mod.OnAuditBegin += x.OnApiBegin;
                mod.OnAuditEnd += x.OnApiEnd;
                mod.OnAuditException += x.OnApiException;
            }
            manager.OnServiceAdded += OnServiceAdded;
            manager.OnServiceRemoved += OnServiceRemoved;
            manager.Register(mod, p.InstanceName, false, typeof(ApiHttpServerModuleParams));
        }


        void OnServiceRemoved(object instance, ServiceInfo info)
        {
            var mod = Mod;
            if (mod.RemoveObject(instance))
                Manager.AddMessage(MsgPrefix + "Removed API methods from type \"" + instance.GetType() + "\" (" + instance + ")", MessageLevels.Debug);
            var x = instance as IApiAuditService;
            if (x != null)
            {
                mod.OnAuditBegin -= x.OnApiBegin;
                mod.OnAuditEnd -= x.OnApiEnd;
                mod.OnAuditException -= x.OnApiException;
            }
        }

        void OnServiceAdded(object instance, ServiceInfo info)
        {
            var mod = Mod;
            if (mod.AddObject(instance))
                Manager.AddMessage(MsgPrefix + "Added API methods from type \"" + instance.GetType() + "\" (" + instance + ")", MessageLevels.Debug);
            var x = instance as IApiAuditService;
            if (x != null)
            {
                mod.OnAuditBegin += x.OnApiBegin;
                mod.OnAuditEnd += x.OnApiEnd;
                mod.OnAuditException += x.OnApiException;
            }
        }

        const String MsgPrefix = "[ApiHttpServerService] ";
        readonly ApiHttpServerModule Mod;
        readonly ServiceManager Manager;

        public void Dispose()
        {
            Manager.Unregister(Mod);
        }

    }

}
