using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Runtime.InteropServices;
using SysWeaver.Net;

namespace SysWeaver
{
    /// <summary>
    /// The network connections (<see cref="INetConnections"/>), read using the IP helper api (GetExtendedTcpTable / GetExtendedUdpTable).
    /// </summary>
    public sealed partial class WindowsPlatformTools : INetConnections
    {
        const int AfInet = 2;
        const int AfInet6 = 23;
        const uint ErrorInsufficientBuffer = 122;

        /// <summary>
        /// TCP_TABLE_OWNER_PID_ALL: all connections and listeners, with the owning process
        /// </summary>
        const int TcpTableOwnerPidAll = 5;

        /// <summary>
        /// TCP_TABLE_OWNER_MODULE_ALL: all connections and listeners, with the owning process and the creation time
        /// </summary>
        const int TcpTableOwnerModuleAll = 8;

        /// <summary>
        /// UDP_TABLE_OWNER_PID: all bound ports, with the owning process
        /// </summary>
        const int UdpTableOwnerPid = 1;

        [DllImport("iphlpapi.dll")]
        static extern uint GetExtendedTcpTable(IntPtr table, ref int size, bool order, int af, int tableClass, uint reserved);

        [DllImport("iphlpapi.dll")]
        static extern uint GetExtendedUdpTable(IntPtr table, ref int size, bool order, int af, int tableClass, uint reserved);

        /// <summary>
        /// 0 = not tried, 1 = the module table works (has the creation time), 2 = use the pid table
        /// </summary>
        static int TcpTableMode;

        /// <summary>
        /// Get all network connections of this computer (TCP connections and listeners, and the bound UDP ports), with the owning process and (for TCP, when Windows knows it) the creation time.
        /// </summary>
        /// <param name="into">The connections are added to this list</param>
        /// <param name="includeUdp">True to include the bound UDP ports</param>
        /// <returns>True if successful, false if the tables couldn't be read (the exception is tracked in the stats)</returns>
        public bool TryGetConnections(List<NetConnection> into, bool includeUdp)
        {
            try
            {
                if (TcpTableMode != 2)
                {
                    try
                    {
                        ReadTcp(into, AfInet, true);
                        ReadTcp(into, AfInet6, true);
                        TcpTableMode = 1;
                    }
                    catch
                    {
                        if (TcpTableMode == 1)
                            throw;
                        //  The module table isn't available, use the pid table from now on
                        into.Clear();
                        TcpTableMode = 2;
                    }
                }
                if (TcpTableMode == 2)
                {
                    ReadTcp(into, AfInet, false);
                    ReadTcp(into, AfInet6, false);
                }
                if (includeUdp)
                {
                    ReadUdp(into, AfInet);
                    ReadUdp(into, AfInet6);
                }
                return true;
            }
            catch (Exception ex)
            {
                Exs.OnException(ex);
                return false;
            }
        }

        /// <summary>
        /// Call the IP helper api until the table fits in the buffer
        /// </summary>
        /// <returns>The table (free with Marshal.FreeHGlobal), IntPtr.Zero if there are no rows</returns>
        static IntPtr GetTable(bool tcp, int af, int tableClass)
        {
            int size = 0;
            IntPtr t = IntPtr.Zero;
            try
            {
                for (int tries = 0; ; ++tries)
                {
                    var res = tcp
                        ? GetExtendedTcpTable(t, ref size, false, af, tableClass, 0)
                        : GetExtendedUdpTable(t, ref size, false, af, tableClass, 0);
                    if (res == 0)
                        break;
                    if ((res != ErrorInsufficientBuffer) || (tries >= 8))
                        throw new Win32Exception((int)res);
                    //  The table may grow between the calls, so allocate some extra
                    if (t != IntPtr.Zero)
                        Marshal.FreeHGlobal(t);
                    t = IntPtr.Zero;
                    size += 4096;
                    t = Marshal.AllocHGlobal(size);
                }
                var r = t;
                t = IntPtr.Zero;
                return r;
            }
            finally
            {
                if (t != IntPtr.Zero)
                    Marshal.FreeHGlobal(t);
            }
        }

        /// <summary>
        /// The port is in network byte order (in the low 16 bits)
        /// </summary>
        static int ReadPort(IntPtr p)
        {
            var v = Marshal.ReadInt32(p);
            return ((v & 0xff) << 8) | ((v >> 8) & 0xff);
        }

        static IPAddress ReadV4(IntPtr p) => new IPAddress((long)(uint)Marshal.ReadInt32(p));

        static IPAddress ReadV6(IntPtr p, byte[] buffer)
        {
            Marshal.Copy(p, buffer, 0, 16);
            return NetConnections.Normalize(new IPAddress(buffer, (uint)Marshal.ReadInt32(p + 16)));
        }

        static void ReadTcp(List<NetConnection> into, int af, bool module)
        {
            var t = GetTable(true, af, module ? TcpTableOwnerModuleAll : TcpTableOwnerPidAll);
            if (t == IntPtr.Zero)
                return;
            try
            {
                var count = Marshal.ReadInt32(t);
                var isV6 = af == AfInet6;
                //  MIB_TCPROW_OWNER_PID (24 bytes), MIB_TCP6ROW_OWNER_PID (56), MIB_TCPROW_OWNER_MODULE (160) and MIB_TCP6ROW_OWNER_MODULE (192),
                //  the module rows have a 64 bit value, so they start at offset 8 (the alignment of the row)
                int rowSize = module ? (isV6 ? 192 : 160) : (isV6 ? 56 : 24);
                int first = module ? 8 : 4;
                var a6 = isV6 ? new byte[16] : null;
                for (int i = 0; i < count; ++i)
                {
                    var row = t + first + i * rowSize;
                    var c = new NetConnection
                    {
                        Protocol = NetConnectionProtocols.Tcp,
                    };
                    long created;
                    if (isV6)
                    {
                        c.LocalAddress = ReadV6(row, a6);
                        c.LocalPort = ReadPort(row + 20);
                        c.RemoteAddress = ReadV6(row + 24, a6);
                        c.RemotePort = ReadPort(row + 44);
                        c.State = NetConnections.FromMibState(Marshal.ReadInt32(row + 48));
                        c.ProcessId = Marshal.ReadInt32(row + 52);
                        created = module ? Marshal.ReadInt64(row + 56) : 0;
                    }
                    else
                    {
                        c.State = NetConnections.FromMibState(Marshal.ReadInt32(row));
                        c.LocalAddress = ReadV4(row + 4);
                        c.LocalPort = ReadPort(row + 8);
                        c.RemoteAddress = ReadV4(row + 12);
                        c.RemotePort = ReadPort(row + 16);
                        c.ProcessId = Marshal.ReadInt32(row + 20);
                        created = module ? Marshal.ReadInt64(row + 24) : 0;
                    }
                    if (c.State == NetConnectionStates.Listen)
                    {
                        c.RemoteAddress = null;
                        c.RemotePort = 0;
                    }
                    //  A FILETIME, 0 if unknown
                    if (created > 0)
                    {
                        try
                        {
                            c.Created = DateTime.FromFileTimeUtc(created);
                        }
                        catch
                        {
                        }
                    }
                    into.Add(c);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(t);
            }
        }

        static void ReadUdp(List<NetConnection> into, int af)
        {
            var t = GetTable(false, af, UdpTableOwnerPid);
            if (t == IntPtr.Zero)
                return;
            try
            {
                var count = Marshal.ReadInt32(t);
                var isV6 = af == AfInet6;
                //  MIB_UDPROW_OWNER_PID (12 bytes) and MIB_UDP6ROW_OWNER_PID (28)
                int rowSize = isV6 ? 28 : 12;
                var a6 = isV6 ? new byte[16] : null;
                for (int i = 0; i < count; ++i)
                {
                    var row = t + 4 + i * rowSize;
                    into.Add(isV6
                        ? new NetConnection
                        {
                            Protocol = NetConnectionProtocols.Udp,
                            LocalAddress = ReadV6(row, a6),
                            LocalPort = ReadPort(row + 20),
                            ProcessId = Marshal.ReadInt32(row + 24),
                        }
                        : new NetConnection
                        {
                            Protocol = NetConnectionProtocols.Udp,
                            LocalAddress = ReadV4(row),
                            LocalPort = ReadPort(row + 4),
                            ProcessId = Marshal.ReadInt32(row + 8),
                        });
                }
            }
            finally
            {
                Marshal.FreeHGlobal(t);
            }
        }
    }
}
