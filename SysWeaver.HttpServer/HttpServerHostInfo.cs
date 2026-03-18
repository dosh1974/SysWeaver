using System;
using System.Collections.Concurrent;

namespace SysWeaver.Net
{
    public sealed class HttpServerHostInfo
    {
        public override string ToString() => Name;

        public readonly String Name;
        public readonly int Len;

//        public HttpServerHostInfo(String name, StringTree prefixes)
        public HttpServerHostInfo(String name, HttpServerPrefix prefix)
        {
            Name = name;
            Prefix = prefix;
            Len = name.Length;
        }
        /// <summary>
        /// Used to find what prefix a request to this host is using
        /// </summary>
        //public readonly StringTree Prefixes;
        public readonly FrozenStringTree Prefixes;

        /// <summary>
        ///  If there is a single prefix this is set
        /// </summary>
        public readonly HttpServerPrefix Prefix;

        /// <summary>
        /// Modules can assign custom data that should be associated with a host
        /// </summary>
        public readonly ConcurrentDictionary<String, Object> Custom = new ConcurrentDictionary<string, object>(StringComparer.Ordinal);
    }



}
