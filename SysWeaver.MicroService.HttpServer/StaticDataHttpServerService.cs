using SysWeaver.Net;
using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace SysWeaver.MicroService
{

    [IsMicroService]
    public sealed class StaticDataHttpServerService : IDisposable
    {
        public override string ToString() => "[Service] " + Mod;

        public StaticDataHttpServerService(ServiceManager manager, StaticDataHttpServerServiceParams p = null)
        {
            Manager = manager;
            p = p ?? new StaticDataHttpServerServiceParams();
            var mod = new StaticDataHttpServerModule(p);
            Mod = mod;
            manager.Register(mod, p.InstanceName, false, typeof(StaticDataHttpServerModuleParams));
            foreach (var x in manager.OrderedUniqueInstances)
                AddFiles(x);
            manager.OnServiceAdded += OnServiceAdded;
        }

        void OnServiceAdded(object arg1, ServiceInfo arg2)
        {
            AddFiles(arg1);
        }

        readonly ConcurrentDictionary<Assembly, int> Asms = new ConcurrentDictionary<Assembly, int>();


        static bool Filter(ref String x)
        {
            var fi = x.IndexOf(".web.", StringComparison.Ordinal);
            if (fi < 0)
                return false;
            fi += 5;
            x = x.Substring(fi);
            return true;
        }

        void AddFiles(Object o)
        {
            var asm  = o.GetType().Assembly;
            if (!Asms.TryAdd(asm, 0))
                return;
            if (asm.GetManifestResourceNames().Length <= 0)
                return;
            Mod.AddEmbeddedResources(asm, null, null, null, null, false, null, null, Filter);
        }


        readonly StaticDataHttpServerModule Mod;
        readonly ServiceManager Manager;

        public void Dispose()
        {
            Manager.Unregister(Mod);
        }

    }

}
