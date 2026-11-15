using System;
using System.Collections.Generic;
using System.Threading;
using SysWeaver.Data;

namespace SysWeaver.ReverseProxy
{
    /// <summary>
    /// Brief information about a client that is connected to a reverse proxy server
    /// </summary>
    public sealed class ReverseProxyClientBrief
    {
        /// <summary>
        /// Create an empty instance (for deserialization)
        /// </summary>
        public ReverseProxyClientBrief()
        {
        }

        internal ReverseProxyClientBrief(KeyValuePair<String, ReverseProxyServer.Client> d, String baseUrl)
        {
            var c = d.Value;
            BaseUrl = baseUrl;
            Client = d.Key;
            LastConnected = new DateTime(Interlocked.Read(ref c.LastConnection), DateTimeKind.Utc);
        }

        /// <summary>
        /// Name of the client (typically machine name)
        /// </summary>
        [TableDataUrl("{0}", "../{1}{0}/")]
        public String Client;

        /// <summary>
        /// The base url to access a client.
        /// </summary>
        [TableDataHide]
        public String BaseUrl;

        /// <summary>
        /// When a connection from the client was last made
        /// </summary>
        public DateTime LastConnected;

    }


}
