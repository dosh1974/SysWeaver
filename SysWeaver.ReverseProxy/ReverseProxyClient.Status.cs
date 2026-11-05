using System;
using System.Threading;
using SysWeaver.Data;
using SysWeaver.MicroService;

namespace SysWeaver.ReverseProxy
{


    public sealed partial class ReverseProxyClient
    {
        /// <summary>
        /// UTC ticks of the last successful long poll to the reverse proxy server (a request was received, or the poll ended without one), 0 = never
        /// </summary>
        long LastOkTicks;

        /// <summary>
        /// Number of long polls currently waiting for the reverse proxy server
        /// </summary>
        int ActivePolls;

        /// <summary>
        /// Call when a long poll to the server succeeded
        /// </summary>
        void OnPollOk() => Interlocked.Exchange(ref LastOkTicks, DateTime.UtcNow.Ticks);

        /// <summary>
        /// The connection status of a reverse proxy client
        /// </summary>
        [TableDataPrimaryKey(nameof(ClientId))]
        public sealed class ConnectionStatus
        {
            /// <summary>
            /// The status: "Connected", "No answer" (no answer from the server for 2 minutes), "Not connected" (the last attempt failed) or "Connecting" (no answer or failure yet)
            /// </summary>
            public String Status;

            /// <summary>
            /// True if the last long poll of the reverse proxy server succeeded less than 2 minutes ago (<see cref="Status"/> is "Connected")
            /// </summary>
            public bool Connected;

            /// <summary>
            /// The client id used on the reverse proxy server
            /// </summary>
            public String ClientId;

            /// <summary>
            /// The base url of the reverse proxy server
            /// </summary>
            [TableDataUrl("{0}", "{0}")]
            public String Server;

            /// <summary>
            /// The base url where this client's end points are reached on the reverse proxy server
            /// </summary>
            [TableDataUrl("{0}", "{0}")]
            public String PublicUrl;

            /// <summary>
            /// The last time (UTC) a long poll to the server succeeded
            /// </summary>
            public DateTime LastOk;

            /// <summary>
            /// The last time (UTC) a connection or poll failed
            /// </summary>
            public DateTime LastFailTime;

            /// <summary>
            /// The last failure (sensitive information removed)
            /// </summary>
            [TableDataText(60)]
            public String LastFail;

            /// <summary>
            /// Number of failed connection attempts (creating the connection)
            /// </summary>
            public long ConnectFails;

            /// <summary>
            /// Number of failed long polls
            /// </summary>
            public long PollFails;

            /// <summary>
            /// Number of long polls currently waiting for a request from the server
            /// </summary>
            public int ActivePolls;

            /// <summary>
            /// Number of workers (concurrent long polls)
            /// </summary>
            public int Workers;
        }

        /// <summary>
        /// Get the current connection status to the reverse proxy server
        /// </summary>
        /// <returns>The status</returns>
        public ConnectionStatus GetStatus()
        {
            var ok = Interlocked.Read(ref LastOkTicks);
            var cf = ConnectFails;
            var pf = ProxyFails;
            var cft = cf.LastTime;
            var pft = pf.LastTime;
            var failTicks = Math.Max(cft, pft);
            var failEx = cft >= pft ? cf.LastException : pf.LastException;
            //  The server answers a long poll within 30 s (with a request or nothing), so no answer for 2 minutes means that the connection hangs
            var connected = (ok > 0) && (ok >= failTicks) && (Server != null) && ((DateTime.UtcNow.Ticks - ok) < (TimeSpan.TicksPerMinute * 2));
            String status;
            if (connected)
                status = "Connected";
            else if ((ok > 0) && (ok >= failTicks))
                status = "No answer";
            else if (failTicks > 0)
                status = "Not connected";
            else
                status = "Connecting";
            var b = ServerBaseUrl;
            return new ConnectionStatus
            {
                Status = status,
                Connected = connected,
                ClientId = ClientId,
                Server = Params.BaseUrl,
                PublicUrl = b.TrimEnd('-') + '/',
                LastOk = ok == 0 ? DateTime.MinValue : new DateTime(ok, DateTimeKind.Utc),
                LastFailTime = failTicks == 0 ? DateTime.MinValue : new DateTime(failTicks, DateTimeKind.Utc),
                LastFail = failEx?.SafeMessage(),
                ConnectFails = cf.Count,
                PollFails = pf.Count,
                ActivePolls = Volatile.Read(ref ActivePolls),
                Workers = ConnectionTasks?.Length ?? 0,
            };
        }

        /// <summary>
        /// Get the current connection status to the reverse proxy server
        /// </summary>
        /// <returns>The status</returns>
        [WebApi("debug/{0}")]
        [WebApiAuth(Roles.DevAdminOps)]
        [WebApiClientCache(1)]
        [WebApiRequestCache(1)]
        public ConnectionStatus GetConnectionStatus() => GetStatus();

        /// <summary>
        /// The connection status to the reverse proxy server
        /// </summary>
        /// <param name="r">Parameters</param>
        /// <returns></returns>
        [WebApi("debug/{0}")]
        [WebApiAuth(Roles.DevAdminOps)]
        [WebApiClientCache(1)]
        [WebApiRequestCache(1)]
        [WebMenuTable(null, "Operations/{0}", "Reverse proxy connection", "Is this client connected to the reverse proxy server?", "icons/network.svg", 30)]
        public TableData ReverseProxyConnectionTable(TableDataRequest r)
            => TableDataTools.Get(r, 2000, [GetStatus()]);

    }

}
