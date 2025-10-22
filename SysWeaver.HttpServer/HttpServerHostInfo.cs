using System;
using System.Collections.Concurrent;

namespace SysWeaver.Net
{
    public sealed class HttpServerHostInfo
    {
        public override string ToString() => Name;

        public readonly String Name;

        public HttpServerHostInfo(String name, StringTree prefixes)
        {
            Name = name;
            Prefixes = prefixes;
        }
        /// <summary>
        /// Used to find what prefix a request to this host is using
        /// </summary>
        public readonly StringTree Prefixes;

        /// <summary>
        /// Modules can assign custom data that should be associated with a host
        /// </summary>
        public readonly ConcurrentDictionary<String, Object> Custom = new ConcurrentDictionary<string, object>(StringComparer.Ordinal);
    }



}
