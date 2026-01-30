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



}
