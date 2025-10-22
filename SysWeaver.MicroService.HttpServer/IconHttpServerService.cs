using SysWeaver.Net;
using SysWeaver.Net.IconModule;
using System;

namespace SysWeaver.MicroService
{

    [IsMicroService]
    [RequiredDep<StaticDataHttpServerModule>]
    public sealed class IconHttpServerService : IDisposable
    {
        public override string ToString() => "[Service] " + Mod.ToString();

        public IconHttpServerService(ServiceManager manager, IconHttpServerServiceParams p = null)
        {
            Manager = manager;
            p = p ?? new IconHttpServerServiceParams();
            var staticData = manager.Get<StaticDataHttpServerModule>(ServiceInstanceTypes.LocalOnly);
            var mod = new IconHttpServerModule(staticData, p);
            Mod = mod;
            manager.Register(mod, p.InstanceName, false, typeof(IconHttpServerModuleParams));
        }

        readonly IconHttpServerModule Mod;
        readonly ServiceManager Manager;

        public void Dispose()
        {
            Manager.Unregister(Mod);
        }

    }

}
