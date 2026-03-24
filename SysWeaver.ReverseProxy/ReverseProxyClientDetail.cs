using System;
using System.Collections.Generic;
using System.Threading;
using SysWeaver.Data;

namespace SysWeaver.ReverseProxy
{
    public sealed class ReverseProxyClientDetail
    {

        public ReverseProxyClientDetail()
        {
        }

        internal ReverseProxyClientDetail(KeyValuePair<String, ReverseProxyServer.Client> d, String baseUrl)
        {
            var c = d.Value;
            BaseUrl = baseUrl;
            Client = d.Key;
            Ip = c.Ip;
            LastConnected = new DateTime(Interlocked.Read(ref c.LastConnection), DateTimeKind.Utc);
            Connections = Interlocked.Read(ref c.Connections);
            Pending = Interlocked.Read(ref c.PendingCount);
            InProgress = Interlocked.Read(ref c.InProgress);
            Sent = Interlocked.Read(ref c.TotalSent);
            Completed = Interlocked.Read(ref c.TotalCompleted);
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
        /// IP of the client (of last the connection made)
        /// </summary>
        [TableDataIp]
        public String Ip;

        /// <summary>
        /// When a connection from the client was last made
        /// </summary>
        public DateTime LastConnected;

        /// <summary>
        /// Number of client connections (ready to process requests)
        /// </summary>
        public long Connections;

        /// <summary>
        /// Number of pending requests (queued, waiting for a connection to process them)
        /// </summary>
        public long Pending;

        /// <summary>
        /// Number of client requests in progress
        /// </summary>
        public long InProgress;

        /// <summary>
        /// Total number of requests sent to the client
        /// </summary>
        public long Sent;

        /// <summary>
        /// Total number of completed client requests
        /// </summary>
        public long Completed;

    }

}
