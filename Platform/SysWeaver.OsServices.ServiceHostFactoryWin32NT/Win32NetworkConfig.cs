using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace SysWeaver.OsServices
{
    /// <summary>
    /// <see cref="INetworkConfig"/> for Windows, adapters from MSFT_NetAdapter (root\StandardCimv2, includes disabled adapters) combined with <see cref="NetworkInterface"/>,
    /// routes from MSFT_NetRoute, the DNS cache is flushed using DnsFlushResolverCache.
    /// </summary>
    sealed class Win32NetworkConfig : INetworkConfig
    {
        public String Name => "Windows network configuration";

        public String HostsFile => System.IO.Path.Combine(Environment.SystemDirectory, "drivers", "etc", "hosts");

        const String Scope = @"root\StandardCimv2";

        static KeyValuePair<String, String> P(String k, String v) => new KeyValuePair<String, String>(k, v);

        static String Prefix(UnicastIPAddressInformation a) => String.Concat(a.Address.ToString(), "/", a.PrefixLength);

        /// <summary>
        /// Fill the ip configuration from a NetworkInterface
        /// </summary>
        static void Fill(OsNetworkAdapter x, NetworkInterface ni)
        {
            try
            {
                var p = ni.GetIPProperties();
                x.IPv4 = p.UnicastAddresses.Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork).Select(Prefix).ToArray();
                x.IPv6 = p.UnicastAddresses.Where(a => a.Address.AddressFamily == AddressFamily.InterNetworkV6).Select(Prefix).ToArray();
                x.Gateways = p.GatewayAddresses.Select(g => g.Address.ToString()).Where(g => (g != "0.0.0.0") && (g != "::")).ToArray();
                x.Dns = p.DnsAddresses.Select(d => d.ToString()).ToArray();
                x.DnsSuffix = p.DnsSuffix ?? "";
                try
                {
                    x.DhcpServers = p.DhcpServerAddresses.Select(d => d.ToString()).ToArray();
                }
                catch
                {
                }
                try
                {
                    var v4 = p.GetIPv4Properties();
                    if (v4 != null)
                    {
                        x.Dhcp = v4.IsDhcpEnabled;
                        if (x.Mtu <= 0)
                            x.Mtu = Math.Max(0, v4.Mtu);
                    }
                }
                catch
                {
                }
            }
            catch
            {
            }
            if (String.IsNullOrEmpty(x.Mac))
            {
                var mac = ni.GetPhysicalAddress().GetAddressBytes();
                x.Mac = mac.Length > 0 ? String.Join("-", mac.Select(b => b.ToString("X2"))) : "";
            }
            if ((x.Speed <= 0) && (ni.OperationalStatus == OperationalStatus.Up))
                x.Speed = Math.Max(0, ni.Speed);
        }

        public IReadOnlyList<OsNetworkAdapter> GetAdapters()
        {
            var nis = NetworkInterface.GetAllNetworkInterfaces().ToDictionary(n => n.Id, StringComparer.OrdinalIgnoreCase);
            var objs = Win32Wmi.TryQuery(Scope, "SELECT * FROM MSFT_NetAdapter");
            var res = new List<OsNetworkAdapter>();
            var seen = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var o in objs)
                {
                    var guid = Win32Wmi.Str(o, "InterfaceGuid");
                    //  Hidden adapters are filter drivers, WAN miniports etc (not shown by Get-NetAdapter either)
                    if (Win32Wmi.Bool(o, "Hidden"))
                    {
                        seen.Add(guid);
                        continue;
                    }
                    var state = Win32Wmi.Long(o, "State");
                    var oper = Win32Wmi.Long(o, "InterfaceOperationalStatus");
                    var media = Win32Wmi.Long(o, "MediaConnectState");
                    var disabled = state == 3;
                    var mac = Win32Wmi.Str(o, "PermanentAddress");
                    if (mac.Length == 12)
                        mac = String.Join("-", Enumerable.Range(0, 6).Select(i => mac.Substring(i * 2, 2)));
                    var x = new OsNetworkAdapter
                    {
                        Id = guid,
                        Name = Win32Wmi.Str(o, "Name"),
                        Description = Win32Wmi.Str(o, "InterfaceDescription"),
                        Type = "",
                        Status = disabled ? "Disabled" : (oper == 1 ? "Up" : (media == 2 ? "Disconnected" : "Down")),
                        Enabled = !disabled,
                        CanDisable = true,
                        Virtual = Win32Wmi.Bool(o, "Virtual"),
                        Mac = mac,
                        Speed = (oper == 1) ? Win32Wmi.Long(o, "ReceiveLinkSpeed") : 0,
                        Mtu = (int)Math.Max(0, Win32Wmi.Long(o, "MtuSize")),
                        Index = (int)Win32Wmi.Long(o, "InterfaceIndex"),
                        IPv4 = [],
                        IPv6 = [],
                        Gateways = [],
                        Dns = [],
                        DnsSuffix = "",
                        DhcpServers = [],
                    };
                    var props = new List<KeyValuePair<String, String>>
                    {
                        P("Interface guid", guid),
                    };
                    var drv = Win32Wmi.Str(o, "DriverDescription");
                    if (drv.Length > 0)
                        props.Add(P("Driver", String.Concat(drv, " ", Win32Wmi.Str(o, "DriverVersionString"), " (", Win32Wmi.Str(o, "DriverProvider"), ")")));
                    var drvDate = Win32Wmi.Str(o, "DriverDate");
                    if (drvDate.Length > 0)
                        props.Add(P("Driver date", drvDate));
                    var pnp = Win32Wmi.Str(o, "PnPDeviceID");
                    if (pnp.Length > 0)
                        props.Add(P("Device id", pnp));
                    props.Add(P("Hardware", Win32Wmi.Bool(o, "HardwareInterface") ? "Yes" : "No"));
                    var phy = Win32Wmi.Long(o, "NdisPhysicalMedium") switch
                    {
                        0 => "Unspecified",
                        1 => "Wireless LAN",
                        8 => "Wireless WAN",
                        9 => "Wireless WAN",
                        10 => "Bluetooth",
                        14 => "Ethernet (802.3)",
                        15 => "IEEE 1394",
                        _ => "",
                    };
                    if (phy.Length > 0)
                        props.Add(P("Physical medium", phy));
                    x.Properties = props.ToArray();
                    if (nis.TryGetValue(guid, out var ni))
                    {
                        x.Type = ni.NetworkInterfaceType.ToString();
                        Fill(x, ni);
                    }
                    else
                    {
                        x.Type = phy;
                    }
                    seen.Add(guid);
                    res.Add(x);
                }
            }
            finally
            {
                Win32Wmi.Dispose(objs);
            }
            //  Interfaces that aren't adapters (ex: loopback), only those with an address (skips filter drivers, WAN miniports etc)
            foreach (var ni in nis.Values)
            {
                if (seen.Contains(ni.Id) || (ni.OperationalStatus == OperationalStatus.NotPresent))
                    continue;
                bool hasAddress = false;
                try
                {
                    hasAddress = ni.GetIPProperties().UnicastAddresses.Count > 0;
                }
                catch
                {
                }
                if (!hasAddress)
                    continue;
                var x = new OsNetworkAdapter
                {
                    Id = ni.Id,
                    Name = ni.Name,
                    Description = ni.Description,
                    Type = ni.NetworkInterfaceType.ToString(),
                    Status = ni.OperationalStatus.ToString(),
                    Enabled = true,
                    CanDisable = false,
                    Virtual = true,
                    Mac = "",
                    IPv4 = [],
                    IPv6 = [],
                    Gateways = [],
                    Dns = [],
                    DnsSuffix = "",
                    DhcpServers = [],
                    Properties = [],
                };
                try
                {
                    x.Index = ni.GetIPProperties().GetIPv4Properties()?.Index ?? 0;
                }
                catch
                {
                }
                Fill(x, ni);
                res.Add(x);
            }
            return res;
        }

        public void SetAdapterEnabled(String id, bool enabled)
        {
            if (String.IsNullOrWhiteSpace(id) || (id.IndexOf('\'') >= 0) || (id.IndexOf('\\') >= 0))
                throw new ArgumentException("Invalid adapter id \"" + id + "\"!");
            var objs = Win32Wmi.Query(Scope, "SELECT * FROM MSFT_NetAdapter WHERE InterfaceGuid = '" + id + "'");
            try
            {
                if (objs.Count <= 0)
                    throw new Exception("The adapter \"" + id + "\" can't be found!");
                var r = objs[0].InvokeMethod(enabled ? "Enable" : "Disable", null);
                var code = Convert.ToInt64(r ?? 0);
                if (code != 0)
                    throw new Exception(String.Concat("Failed to ", enabled ? "enable" : "disable", " the adapter \"", Win32Wmi.Str(objs[0], "Name"), "\": ", code == 5 ? "Access denied (requires admin)" : ("error " + code)));
            }
            finally
            {
                Win32Wmi.Dispose(objs);
            }
        }

        static String Protocol(long p) => p switch
        {
            1 => "Other",
            2 => "Local",
            3 => "Static",
            4 => "ICMP",
            8 => "RIP",
            13 => "OSPF",
            14 => "BGP",
            19 => "DHCP",
            _ => p.ToString(),
        };

        public IReadOnlyList<OsRoute> GetRoutes()
        {
            var routes = Win32Wmi.Query(Scope, "SELECT * FROM MSFT_NetRoute");
            var ifs = Win32Wmi.TryQuery(Scope, "SELECT InterfaceIndex, AddressFamily, InterfaceMetric FROM MSFT_NetIPInterface");
            try
            {
                var metrics = new Dictionary<(long, long), long>();
                foreach (var i in ifs)
                    metrics[(Win32Wmi.Long(i, "InterfaceIndex"), Win32Wmi.Long(i, "AddressFamily"))] = Win32Wmi.Long(i, "InterfaceMetric");
                var res = new Dictionary<(String, String, long), OsRoute>();
                foreach (var r in routes)
                {
                    var dest = Win32Wmi.Str(r, "DestinationPrefix");
                    var hop = Win32Wmi.Str(r, "NextHop");
                    var idx = Win32Wmi.Long(r, "InterfaceIndex");
                    var fam = Win32Wmi.Long(r, "AddressFamily");
                    var persistent = Win32Wmi.Long(r, "Store") == 0;
                    var key = (dest, hop, idx);
                    if (res.TryGetValue(key, out var e))
                    {
                        if (persistent)
                            e.Persistent = true;
                        continue;
                    }
                    var metric = Win32Wmi.Long(r, "RouteMetric");
                    if (metrics.TryGetValue((idx, fam), out var im))
                        metric += im;
                    res[key] = new OsRoute
                    {
                        Destination = dest,
                        Gateway = (hop == "0.0.0.0") || (hop == "::") ? "" : hop,
                        Interface = Win32Wmi.Str(r, "InterfaceAlias"),
                        Metric = (int)Math.Min(int.MaxValue, metric),
                        Family = fam == 23 ? "IPv6" : "IPv4",
                        Protocol = Protocol(Win32Wmi.Long(r, "Protocol")),
                        Table = "",
                        Persistent = persistent,
                    };
                }
                return res.Values.ToList();
            }
            finally
            {
                Win32Wmi.Dispose(routes);
                Win32Wmi.Dispose(ifs);
            }
        }

        [DllImport("dnsapi.dll", EntryPoint = "DnsFlushResolverCache")]
        static extern int DnsFlushResolverCache();

        public String FlushDns()
        {
            if (DnsFlushResolverCache() == 0)
                throw new Exception("Failed to flush the DNS resolver cache (is the DNS Client service running?)");
            return "The DNS resolver cache was flushed";
        }
    }
}
