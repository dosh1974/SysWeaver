using System;
using SysWeaver.Data;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// A network connection of the server (a row of <see cref="ServerManagerService.NetworkConnectionsTable"/>, like a line of netstat)
    /// </summary>
    [TableDataPrimaryKey(nameof(Protocol), nameof(LocalAddress), nameof(LocalPort), nameof(RemoteAddress), nameof(RemotePort))]
    [TableDataDefaultOrder(nameof(Protocol), nameof(LocalPort))]
    public sealed class SmNetConnection
    {
        /// <summary>
        /// The protocol, "TCP" or "UDP"
        /// </summary>
        [TableDataOrder(1)]
        public String Protocol;

        /// <summary>
        /// The state of the connection (ex: "Established", "Listen"), empty for UDP
        /// </summary>
        [TableDataOrder(2)]
        public String State;

        /// <summary>
        /// "Incoming" (a connection to a local listening port), "Outgoing", "Listening" (a TCP listener) or "Bound" (a UDP port)
        /// </summary>
        [TableDataOrder(3)]
        public String Direction;

        /// <summary>
        /// The local address
        /// </summary>
        [TableDataOrder(4)]
        [TableDataIp]
        public String LocalAddress;

        /// <summary>
        /// The local port
        /// </summary>
        [TableDataOrder(5)]
        [TableDataNumber]
        public int LocalPort;

        /// <summary>
        /// The remote address, empty for a listener or a UDP port
        /// </summary>
        [TableDataOrder(6)]
        [TableDataIp]
        public String RemoteAddress;

        /// <summary>
        /// The remote port, 0 for a listener or a UDP port
        /// </summary>
        [TableDataOrder(7)]
        [TableDataNumber]
        public int RemotePort;

        /// <summary>
        /// "Local" (this computer), "LAN" (a local network) or "Internet" (an external address), empty without a remote address
        /// </summary>
        [TableDataOrder(8)]
        public String Remote;

        /// <summary>
        /// The id of the process that owns the connection, 0 if unknown
        /// </summary>
        [TableDataOrder(9)]
        public int ProcessId;

        /// <summary>
        /// The name of the process that owns the connection, empty if unknown
        /// </summary>
        [TableDataOrder(10)]
        public String ProcessName;

        /// <summary>
        /// How long the connection has been open (since it was created, or since it was first seen when the OS doesn't tell)
        /// </summary>
        [TableDataOrder(11)]
        [TableDataDuration]
        public TimeSpan Age;

        /// <summary>
        /// When the connection was created (UTC), or when it was first seen if the OS doesn't tell
        /// </summary>
        [TableDataOrder(12)]
        [TableDataHide]
        public DateTime Since;

        /// <summary>
        /// The location of an external remote address (empty for local and LAN addresses, or when no ip location service is available)
        /// </summary>
        [TableDataExpand("{0}", "")]
        public SmIpInfo Ip;
    }
}
