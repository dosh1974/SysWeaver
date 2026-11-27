using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace SysWeaver.Net
{
    /// <summary>
    /// The protocol of a network connection (see <see cref="NetConnection"/>)
    /// </summary>
    public enum NetConnectionProtocols
    {
        /// <summary>
        /// A TCP connection (or listener)
        /// </summary>
        Tcp,
        /// <summary>
        /// A bound UDP port (UDP is connection less, there is no remote end point)
        /// </summary>
        Udp,
    }

    /// <summary>
    /// The state of a network connection (the TCP states, see RFC 793)
    /// </summary>
    public enum NetConnectionStates
    {
        /// <summary>
        /// Unknown (or a UDP port, it has no state)
        /// </summary>
        Unknown,
        /// <summary>
        /// Closed
        /// </summary>
        Closed,
        /// <summary>
        /// Waiting for incoming connections (a listener)
        /// </summary>
        Listen,
        /// <summary>
        /// A connection request was sent, waiting for the answer
        /// </summary>
        SynSent,
        /// <summary>
        /// A connection request was received and answered, waiting for the confirmation
        /// </summary>
        SynReceived,
        /// <summary>
        /// An open connection, data can be sent both ways
        /// </summary>
        Established,
        /// <summary>
        /// Closing: waiting for the remote end to confirm (or request) the close
        /// </summary>
        FinWait1,
        /// <summary>
        /// Closing: waiting for the remote end to close
        /// </summary>
        FinWait2,
        /// <summary>
        /// The remote end closed, waiting for the local end to close
        /// </summary>
        CloseWait,
        /// <summary>
        /// Both ends are closing at the same time
        /// </summary>
        Closing,
        /// <summary>
        /// Waiting for the confirmation of the close
        /// </summary>
        LastAck,
        /// <summary>
        /// Closed, waiting for delayed packets before the port can be reused
        /// </summary>
        TimeWait,
        /// <summary>
        /// The connection is being deleted
        /// </summary>
        DeleteTcb,
    }

    /// <summary>
    /// A network connection (or listener / bound port) of this computer, like a line of netstat
    /// </summary>
    public sealed class NetConnection
    {
        /// <inheritdoc/>
        public override string ToString() => String.Concat(Protocol.ToString(), " ", LocalAddress?.ToString(), ":", LocalPort.ToString(), " ", RemoteAddress?.ToString(), ":", RemotePort.ToString(), " ", State.ToString(), " #", ProcessId.ToString());

        /// <summary>
        /// The protocol
        /// </summary>
        public NetConnectionProtocols Protocol;

        /// <summary>
        /// The local address (an IPv4 address mapped to IPv6 is returned as IPv4)
        /// </summary>
        public IPAddress LocalAddress;

        /// <summary>
        /// The local port
        /// </summary>
        public int LocalPort;

        /// <summary>
        /// The remote address, null for a listener or a UDP port (an IPv4 address mapped to IPv6 is returned as IPv4)
        /// </summary>
        public IPAddress RemoteAddress;

        /// <summary>
        /// The remote port, 0 for a listener or a UDP port
        /// </summary>
        public int RemotePort;

        /// <summary>
        /// The state of the connection (<see cref="NetConnectionStates.Unknown"/> for UDP)
        /// </summary>
        public NetConnectionStates State;

        /// <summary>
        /// The id of the process that owns the connection, 0 if unknown
        /// </summary>
        public int ProcessId;

        /// <summary>
        /// When the connection was created (UTC), default if unknown (not every platform tells)
        /// </summary>
        public DateTime Created;
    }

    /// <summary>
    /// Reads the network connections of this computer (implemented by the platform tools, see <see cref="NetConnections"/>)
    /// </summary>
    public interface INetConnections
    {
        /// <summary>
        /// Get all network connections of this computer (TCP connections and listeners, and the bound UDP ports), with the owning process
        /// </summary>
        /// <param name="into">The connections are added to this list</param>
        /// <param name="includeUdp">True to include the bound UDP ports</param>
        /// <returns>True if successful, false if not supported (nothing was added)</returns>
        bool TryGetConnections(List<NetConnection> into, bool includeUdp);
    }

    /// <summary>
    /// The network connections of this computer (like netstat), and address classification
    /// </summary>
    /// <remarks>
    /// The connections are read using the platform tools (<see cref="PlatformTools.Current"/>) when they implement <see cref="INetConnections"/>
    /// (SysWeaver.Common.Windows: the IP helper api, with the owning process and the creation time; SysWeaver.Common.Linux: /proc/net, with the owning process when it can be read).
    /// Else the .NET network information is used (no owning process).
    /// </remarks>
    public static class NetConnections
    {
        /// <summary>
        /// True if the platform tools read the connections (with the owning process), false if the .NET fallback is used
        /// </summary>
        public static bool HavePlatformSupport => PlatformTools.Current is INetConnections;

        /// <summary>
        /// Get all network connections of this computer
        /// </summary>
        /// <param name="includeUdp">True to include the bound UDP ports</param>
        /// <returns>The connections</returns>
        public static List<NetConnection> Get(bool includeUdp = true)
        {
            var list = new List<NetConnection>();
            if ((PlatformTools.Current is INetConnections p) && p.TryGetConnections(list, includeUdp))
                return list;
            list.Clear();
            GetManaged(list, includeUdp);
            return list;
        }

        /// <summary>
        /// Read the connections using the .NET network information (no owning process or creation time)
        /// </summary>
        static void GetManaged(List<NetConnection> list, bool includeUdp)
        {
            var g = IPGlobalProperties.GetIPGlobalProperties();
            foreach (var c in g.GetActiveTcpConnections())
                list.Add(new NetConnection
                {
                    Protocol = NetConnectionProtocols.Tcp,
                    LocalAddress = Normalize(c.LocalEndPoint.Address),
                    LocalPort = c.LocalEndPoint.Port,
                    RemoteAddress = Normalize(c.RemoteEndPoint.Address),
                    RemotePort = c.RemoteEndPoint.Port,
                    State = FromTcpState(c.State),
                });
            foreach (var l in g.GetActiveTcpListeners())
                list.Add(new NetConnection
                {
                    Protocol = NetConnectionProtocols.Tcp,
                    LocalAddress = Normalize(l.Address),
                    LocalPort = l.Port,
                    State = NetConnectionStates.Listen,
                });
            if (!includeUdp)
                return;
            foreach (var l in g.GetActiveUdpListeners())
                list.Add(new NetConnection
                {
                    Protocol = NetConnectionProtocols.Udp,
                    LocalAddress = Normalize(l.Address),
                    LocalPort = l.Port,
                });
        }

        static NetConnectionStates FromTcpState(TcpState s) => s switch
        {
            TcpState.Closed => NetConnectionStates.Closed,
            TcpState.Listen => NetConnectionStates.Listen,
            TcpState.SynSent => NetConnectionStates.SynSent,
            TcpState.SynReceived => NetConnectionStates.SynReceived,
            TcpState.Established => NetConnectionStates.Established,
            TcpState.FinWait1 => NetConnectionStates.FinWait1,
            TcpState.FinWait2 => NetConnectionStates.FinWait2,
            TcpState.CloseWait => NetConnectionStates.CloseWait,
            TcpState.Closing => NetConnectionStates.Closing,
            TcpState.LastAck => NetConnectionStates.LastAck,
            TcpState.TimeWait => NetConnectionStates.TimeWait,
            TcpState.DeleteTcb => NetConnectionStates.DeleteTcb,
            _ => NetConnectionStates.Unknown,
        };

        /// <summary>
        /// Get the state from the number used by the IP helper api of Windows (MIB_TCP_STATE) and by the TCP state of Linux (/proc/net/tcp uses its own numbers, see <see cref="FromLinuxState"/>)
        /// </summary>
        /// <param name="mibState">The MIB_TCP_STATE value (1 = closed ... 12 = delete tcb)</param>
        /// <returns>The state</returns>
        public static NetConnectionStates FromMibState(int mibState)
            => ((mibState >= 1) && (mibState <= 12)) ? (NetConnectionStates)mibState : NetConnectionStates.Unknown;

        /// <summary>
        /// Get the state from the number used in /proc/net/tcp on Linux (1 = established ... 11 = closing)
        /// </summary>
        /// <param name="linuxState">The state number</param>
        /// <returns>The state</returns>
        public static NetConnectionStates FromLinuxState(int linuxState) => linuxState switch
        {
            1 => NetConnectionStates.Established,
            2 => NetConnectionStates.SynSent,
            3 => NetConnectionStates.SynReceived,
            4 => NetConnectionStates.FinWait1,
            5 => NetConnectionStates.FinWait2,
            6 => NetConnectionStates.TimeWait,
            7 => NetConnectionStates.Closed,
            8 => NetConnectionStates.CloseWait,
            9 => NetConnectionStates.LastAck,
            10 => NetConnectionStates.Listen,
            11 => NetConnectionStates.Closing,
            _ => NetConnectionStates.Unknown,
        };

        /// <summary>
        /// Get an address where an IPv4 address mapped to IPv6 is returned as IPv4
        /// </summary>
        /// <param name="a">The address (may be null)</param>
        /// <returns>The address</returns>
        public static IPAddress Normalize(IPAddress a) => ((a != null) && a.IsIPv4MappedToIPv6) ? a.MapToIPv4() : a;

        /// <summary>
        /// Test if an address is local or on a local network (not an internet address): unspecified, loopback, private (RFC 1918, IPv6 unique local),
        /// link local, carrier grade NAT (100.64.0.0/10), multicast, broadcast and reserved addresses
        /// </summary>
        /// <param name="a">The address</param>
        /// <returns>True if the address is local or on a local network (or null), false for an internet (public) address</returns>
        public static bool IsLocalOrLan(IPAddress a)
        {
            a = Normalize(a);
            if (a == null)
                return true;
            if (a.Equals(IPAddress.Any) || a.Equals(IPAddress.IPv6Any) || a.Equals(IPAddress.None) || IPAddress.IsLoopback(a))
                return true;
            if (a.AddressFamily == AddressFamily.InterNetwork)
            {
                Span<Byte> b = stackalloc Byte[4];
                a.TryWriteBytes(b, out _);
                return (b[0] == 0)
                    || (b[0] == 10)
                    || ((b[0] == 172) && ((b[1] & 0xf0) == 16))
                    || ((b[0] == 192) && (b[1] == 168))
                    || ((b[0] == 169) && (b[1] == 254))
                    || ((b[0] == 100) && ((b[1] & 0xc0) == 64))
                    || (b[0] >= 224);
            }
            if (a.AddressFamily == AddressFamily.InterNetworkV6)
                return a.IsIPv6LinkLocal || a.IsIPv6SiteLocal || a.IsIPv6UniqueLocal || a.IsIPv6Multicast;
            return true;
        }
    }
}
