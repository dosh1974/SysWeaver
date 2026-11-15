using System;
using System.Threading;
using SysWeaver.Net;
using SysWeaver.Net.IsoDataModule;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Registers an http server module that serves ISO data (see <see cref="IsoDataHttpServerModule"/>)
    /// </summary>
    [IsMicroService]
    [OptionalDep<StaticDataHttpServerModule>()]
    public sealed class IsoDataService : IDisposable
    {

        /// <inheritdoc />
        public override string ToString() => "[Service] " + Mod;

        /// <summary>
        /// Create the service and register the module
        /// </summary>
        /// <param name="manager">The service manager (the <see cref="StaticDataHttpServerModule"/> is optional)</param>
        public IsoDataService(ServiceManager manager)
        {
            Manager = manager;
            var m = manager.TryGet<StaticDataHttpServerModule>();
            Mod = new IsoDataHttpServerModule(m);
            manager.Register(Mod, null, false);
        }
        readonly ServiceManager Manager;

        IsoDataHttpServerModule Mod;

        /// <inheritdoc />
        public void Dispose()
        {
            var m = Interlocked.Exchange(ref Mod, null);
            if (m != null)
                Manager.Unregister(m);
        }

    }


}
