using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using SysWeaver.Net;

namespace SysWeaver
{
    /// <summary>
    /// The network connections (<see cref="INetConnections"/>), read from /proc/net/tcp, tcp6, udp and udp6.
    /// The owning process is found by matching the socket inodes with the open files of the processes (/proc/[pid]/fd),
    /// only the processes that this process may inspect are found (all of them when running as root).
    /// Linux doesn't tell when a connection was created (<see cref="NetConnection.Created"/> is default).
    /// </summary>
    public sealed partial class LinuxPlatformTools : INetConnections
    {
        /// <summary>
        /// Get all network connections of this computer (TCP connections and listeners, and the bound UDP ports), with the owning process (if it can be found).
        /// </summary>
        /// <param name="into">The connections are added to this list</param>
        /// <param name="includeUdp">True to include the bound UDP ports</param>
        /// <returns>True if successful, false if /proc/net couldn't be read (the exception is tracked in the stats)</returns>
        public bool TryGetConnections(List<NetConnection> into, bool includeUdp)
        {
            try
            {
                var inodes = new List<long>();
                var start = into.Count;
                Read(into, inodes, "/proc/net/tcp", NetConnectionProtocols.Tcp, false);
                Read(into, inodes, "/proc/net/tcp6", NetConnectionProtocols.Tcp, true);
                if (includeUdp)
                {
                    Read(into, inodes, "/proc/net/udp", NetConnectionProtocols.Udp, false);
                    Read(into, inodes, "/proc/net/udp6", NetConnectionProtocols.Udp, true);
                }
                var pids = GetSocketOwners();
                if (pids != null)
                {
                    for (int i = 0; i < inodes.Count; ++i)
                        if (pids.TryGetValue(inodes[i], out var pid))
                            into[start + i].ProcessId = pid;
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
        /// Read a /proc/net file, ex: "  0: 0100007F:1F90 00000000:0000 0A 00000000:00000000 00:00000000 00000000  1000        0 12345 1 ..."
        /// (sl, local address, remote address, state, queues, timer, retransmits, uid, timeout, inode)
        /// </summary>
        static void Read(List<NetConnection> into, List<long> inodes, String file, NetConnectionProtocols protocol, bool v6)
        {
            if (!File.Exists(file))
                return;
            bool header = true;
            foreach (var line in File.ReadLines(file))
            {
                if (header)
                {
                    header = false;
                    continue;
                }
                var p = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (p.Length < 10)
                    continue;
                if (!TryParseEndPoint(p[1], v6, out var la, out var lp))
                    continue;
                if (!TryParseEndPoint(p[2], v6, out var ra, out var rp))
                    continue;
                if (!Int32.TryParse(p[3], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var st))
                    continue;
                Int64.TryParse(p[9], NumberStyles.Integer, CultureInfo.InvariantCulture, out var inode);
                var c = new NetConnection
                {
                    Protocol = protocol,
                    LocalAddress = la,
                    LocalPort = lp,
                };
                if (protocol == NetConnectionProtocols.Tcp)
                {
                    c.State = NetConnections.FromLinuxState(st);
                    if (c.State != NetConnectionStates.Listen)
                    {
                        c.RemoteAddress = ra;
                        c.RemotePort = rp;
                    }
                }
                into.Add(c);
                inodes.Add(inode);
            }
        }

        /// <summary>
        /// Parse "0100007F:1F90" (IPv4) or "00000000000000000000000001000000:1F90" (IPv6): the address is written as 32 bit words in host byte order (little endian), the port in hex
        /// </summary>
        static bool TryParseEndPoint(String s, bool v6, out IPAddress address, out int port)
        {
            address = null;
            port = 0;
            var i = s.IndexOf(':');
            if (i < 0)
                return false;
            if (!Int32.TryParse(s.AsSpan(i + 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out port))
                return false;
            var hex = s.AsSpan(0, i);
            var len = v6 ? 32 : 8;
            if (hex.Length != len)
                return false;
            Span<Byte> b = stackalloc Byte[16];
            var words = len / 8;
            for (int w = 0; w < words; ++w)
            {
                if (!UInt32.TryParse(hex.Slice(w * 8, 8), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v))
                    return false;
                //  The word is in host byte order (little endian on all supported platforms)
                var o = w * 4;
                b[o] = (Byte)v;
                b[o + 1] = (Byte)(v >> 8);
                b[o + 2] = (Byte)(v >> 16);
                b[o + 3] = (Byte)(v >> 24);
            }
            address = NetConnections.Normalize(new IPAddress(b.Slice(0, words * 4)));
            return true;
        }

        /// <summary>
        /// Map socket inodes to the process that has them open (from the "socket:[inode]" links in /proc/[pid]/fd)
        /// </summary>
        /// <returns>The owners, null if /proc can't be read</returns>
        static Dictionary<long, int> GetSocketOwners()
        {
            Dictionary<long, int> d = new();
            String[] procs;
            try
            {
                procs = Directory.GetDirectories("/proc");
            }
            catch
            {
                return null;
            }
            foreach (var pd in procs)
            {
                if (!Int32.TryParse(Path.GetFileName(pd), NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid))
                    continue;
                String[] fds;
                try
                {
                    fds = Directory.GetFiles(Path.Combine(pd, "fd"));
                }
                catch
                {
                    //  Not allowed (another user's process) or ended
                    continue;
                }
                foreach (var fd in fds)
                {
                    try
                    {
                        var target = new FileInfo(fd).LinkTarget;
                        if ((target == null) || !target.StartsWith("socket:[", StringComparison.Ordinal) || !target.EndsWith(']'))
                            continue;
                        if (Int64.TryParse(target.AsSpan(8, target.Length - 9), NumberStyles.Integer, CultureInfo.InvariantCulture, out var inode))
                            d.TryAdd(inode, pid);
                    }
                    catch
                    {
                    }
                }
            }
            return d;
        }
    }
}
