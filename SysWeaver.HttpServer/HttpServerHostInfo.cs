using System;
using System.Collections.Concurrent;

namespace SysWeaver.Net
{
    /// <summary>
    /// A host that the server has received requests for, resolved by <see cref="HttpServerHosts"/> from the "scheme://host:port" part of the request url.
    /// </summary>
    /// <remarks>
    /// For a wildcard listener prefix (ex: "http://*:80/") a new host is created for every distinct host name that clients send (the Host header),
    /// with the "*" replaced by that host name, so <see cref="Name"/> is client controlled for wildcard prefixes.
    /// Instances are kept for the lifetime of the server.
    /// </remarks>
    public sealed class HttpServerHostInfo
    {
        /// <inheritdoc/>
        public override string ToString() => Name;

        /// <summary>
        /// The prefix of the host, the listener prefix with "*" replaced by the requested host name, ex: "http://localhost:8080/".
        /// This is the value of <see cref="HttpServerRequest.Prefix"/> for requests to this host.
        /// </summary>
        public readonly String Name;
        /// <summary>
        /// The length of <see cref="Name"/>.
        /// </summary>
        public readonly int Len;

//        public HttpServerHostInfo(String name, StringTree prefixes)
        /// <summary>
        /// Create a host.
        /// </summary>
        /// <param name="name">The prefix of the host (listener prefix with the wildcard replaced)</param>
        /// <param name="prefix">The listener prefix that the host belongs to</param>
        public HttpServerHostInfo(String name, HttpServerPrefix prefix)
        {
            Name = name;
            Prefix = prefix;
            Len = name.Length;
        }
        /// <summary>
        /// Legacy, used to find what prefix a request to this host is using. Never assigned (always null), use <see cref="Prefix"/>.
        /// </summary>
        //public readonly StringTree Prefixes;
        public readonly FrozenStringTree Prefixes;

        /// <summary>
        /// The listener prefix that this host belongs to.
        /// </summary>
        public readonly HttpServerPrefix Prefix;

        /// <summary>
        /// Modules can assign custom data that should be associated with a host (thread safe).
        /// </summary>
        public readonly ConcurrentDictionary<String, Object> Custom = new ConcurrentDictionary<string, object>(StringComparer.Ordinal);
    }



}
