using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SysWeaver.MicroService
{
    public sealed class ServiceManifest
    {
#if DEBUG
        public override string ToString() => String.Concat(Type.ToQuoted(), " {", Params, "}");
#endif//DEBUG

        public ServiceManifest()
        {
        }

        /// <summary>
        /// Fully qualified type name of the service type
        /// </summary>
        public String Type { get; set; }

        /// <summary>
        /// Optional name of the instance
        /// </summary>
        public String Name { get; set; }

        /// <summary>
        /// Optional parameters
        /// </summary>
        public Object Params { get; set; }


        public bool TryGetParamAs(out Object a, Type pt)
        {
            var op = Params;
            a = null;
            if (op == null)
                return false;
            var opt = op.GetType();
            if (pt.IsAssignableFrom(opt))
            {
                a = op;
                return true;
            }
            if (op is JsonElement)
            {
                try
                {
                    a = JsonSerializer.Deserialize((JsonElement)op, pt, ServiceManager.DeSerOpt);
                    return true;
                }
                catch
                {
                }
            }
            if (op is JsonNode)
            {
                try
                {
                    a = JsonSerializer.Deserialize((JsonNode)op, pt, ServiceManager.DeSerOpt);
                    return true;
                }
                catch
                {
                }
            }
            return false;
        }

    }


    public class ConfigEntry
    {

        /// <summary>
        /// Fully qualified type name of the service type
        /// </summary>
        public String Type;

        /// <summary>
        /// Optional name of the instance
        /// </summary>
        [EditDefault(null)]
        [EditAllowNull]
        public String Name;


        public virtual Object GetParams() => null;
    }


    public class ConfigEntryP<T> : ConfigEntry
    {
        /// <summary>
        /// Parameters
        /// </summary>
        public T Params;


        public ConfigEntryP()
        {
        }

        public ConfigEntryP(T p)
        {
            Params = p;
        }

        public override Object GetParams() => Params;
    }

    public class ConfigEntryOP<T> : ConfigEntry
    {
        /// <summary>
        /// Optional parameters
        /// </summary>
        [EditAllowNull]
        public T Params;

        public ConfigEntryOP()
        {
        }

        public ConfigEntryOP(T p)
        {
            Params = p;
        }

        public override Object GetParams() => Params;
    }



}
