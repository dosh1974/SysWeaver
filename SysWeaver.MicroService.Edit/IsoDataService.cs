using System;
using System.Threading;
using SysWeaver.Net;
using SysWeaver.Net.IsoDataModule;

namespace SysWeaver.MicroService
{
    [IsMicroService]
    [OptionalDep<StaticDataHttpServerModule>()]
    public sealed class IsoDataService : IDisposable
    {

        public override string ToString() => "[Service] " + Mod;

        public IsoDataService(ServiceManager manager)
        {
            Manager = manager;
            var m = manager.TryGet<StaticDataHttpServerModule>();
            Mod = new IsoDataHttpServerModule(m);
            manager.Register(Mod, null, false);
        }
        readonly ServiceManager Manager;

        IsoDataHttpServerModule Mod;

        public void Dispose()
        {
            var m = Interlocked.Exchange(ref Mod, null);
            if (m != null)
                Manager.Unregister(m);
        }

    }


}
