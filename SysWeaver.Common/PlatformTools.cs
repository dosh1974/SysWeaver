using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SysWeaver
{
    public static class PlatformTools
    {
        static readonly IPlatformTools Dummy = new DummyPlatformTools("Unknown");

        static IPlatformTools Get(String p)
        {
            if (p == null)
                return Dummy;
            var name = p.FastToLower().MakeFirstUppercase();
            var tools = Tools;
            if (tools.TryGetValue(name, out var tool))
                return tool;
            lock (tools)
            {
                if (tools.TryGetValue(name, out tool))
                    return tool;
                var t = typeof(PlatformTools);
                var asmName = String.Join('.', t.Assembly.GetName().Name, name);
                var className = String.Concat(t.Namespace, ".", name, "PlatformTools");
                var typeName = String.Join(", ", className, asmName);
                Type type = null;
                try
                {
                    type = TypeFinder.Get(typeName);
                    if (type == null)
                    {
                        tool = new DummyPlatformTools(name + " [Type not found]");
                        tools.TryAdd(name, tool);
                        return tool;
                    }
                    tool = Activator.CreateInstance(type) as IPlatformTools;
                    tools.TryAdd(name, tool);
                    return tool;
                }
                catch (Exception ex)
                {
                    if (type == null)
                    {
                        tool = new DummyPlatformTools(String.Concat(name, " [Type failed: ", ex.Message, ']'));
                        tools.TryAdd(name, tool);
                        return tool;
                    }
                    tool = new DummyPlatformTools(String.Concat(name, " [New failed: ", ex.Message, ']'));
                    tools.TryAdd(name, tool);
                    return tool;
                }
            }
        }

        static readonly ConcurrentDictionary<String, IPlatformTools> Tools = new ConcurrentDictionary<string, IPlatformTools>(StringComparer.Ordinal);

        /// <summary>
        /// The platform tools for the OS that the current process is running under.
        /// </summary>
        public static readonly IPlatformTools Current = Get(EnvInfo.OsPlatform);

    }


    public interface IPlatformTools
    {
        String Name { get; }

        bool FlushToDisc(SafeHandle h);

    }

    public sealed class DummyPlatformTools : IPlatformTools
    {
        public DummyPlatformTools(String name)
        {
            Name = name;
        }

        public string Name { get; init; }

        public bool FlushToDisc(SafeHandle h) => true;
    }


}
