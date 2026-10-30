using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;

namespace SysWeaver.OsServices
{
    /// <summary>
    /// <see cref="INetworkConfig"/> for Linux: adapters from <see cref="NetworkInterface"/> and /sys/class/net, "ip -j" for the routes (and dynamic addresses),
    /// "ip link set" to enable / disable, resolvectl (or nscd) to flush the DNS cache.
    /// </summary>
    sealed class UnixNetworkConfig : INetworkConfig
    {
        public UnixNetworkConfig(String ip)
        {
            Ip = ip;
        }

        readonly String Ip;

        public String Name => "Linux network configuration";

        public String HostsFile => "/etc/hosts";

        static String Sys(String name, String file)
        {
            try
            {
                return File.ReadAllText(Path.Combine("/sys/class/net", name, file)).Trim();
            }
            catch
            {
                return "";
            }
        }

        static KeyValuePair<String, String> P(String k, String v) => new KeyValuePair<String, String>(k, v);

        static String Prefix(UnicastIPAddressInformation a) => String.Concat(a.Address.ToString(), "/", a.PrefixLength);

        /// <summary>
        /// Interfaces that have a dynamic (DHCP / SLAAC) IPv4 address, from "ip -j addr"
        /// </summary>
        HashSet<String> Dynamic()
        {
            var res = new HashSet<String>(StringComparer.Ordinal);
            if (Ip == null)
                return res;
            try
            {
                using var doc = JsonDocument.Parse(UnixCommand.Get(Ip, "-j", "addr"));
                foreach (var i in doc.RootElement.EnumerateArray())
                {
                    if (!i.TryGetProperty("ifname", out var n) || !i.TryGetProperty("addr_info", out var ai))
                        continue;
                    foreach (var a in ai.EnumerateArray())
                        if (a.TryGetProperty("family", out var f) && (f.GetString() == "inet") && a.TryGetProperty("dynamic", out var d) && (d.ValueKind == JsonValueKind.True))
                            res.Add(n.GetString());
                }
            }
            catch
            {
            }
            return res;
        }

        public IReadOnlyList<OsNetworkAdapter> GetAdapters()
        {
            var dyn = Dynamic();
            var res = new List<OsNetworkAdapter>();
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                var name = ni.Name;
                var flags = Sys(name, "flags");
                var up = flags.StartsWith("0x", StringComparison.Ordinal) && int.TryParse(flags.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var fl) ? ((fl & 1) != 0) : (ni.OperationalStatus == OperationalStatus.Up);
                var oper = Sys(name, "operstate");
                var isVirtual = !Directory.Exists(Path.Combine("/sys/class/net", name, "device"));
                long.TryParse(Sys(name, "speed"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var speed);
                int.TryParse(Sys(name, "mtu"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var mtu);
                var mac = ni.GetPhysicalAddress().GetAddressBytes();
                var x = new OsNetworkAdapter
                {
                    Id = name,
                    Name = name,
                    Description = "",
                    Type = ni.NetworkInterfaceType.ToString(),
                    Status = !up ? "Disabled" : (oper switch
                    {
                        "up" => "Up",
                        "down" => "Down",
                        "lowerlayerdown" => "Disconnected",
                        "dormant" => "Dormant",
                        _ => ni.OperationalStatus.ToString(),
                    }),
                    Enabled = up,
                    CanDisable = ni.NetworkInterfaceType != NetworkInterfaceType.Loopback,
                    Virtual = isVirtual,
                    Mac = mac.Length > 0 ? String.Join("-", mac.Select(b => b.ToString("X2"))) : "",
                    Speed = speed > 0 ? speed * 1000000 : 0,
                    Mtu = mtu,
                    Dhcp = dyn.Contains(name),
                    IPv4 = [],
                    IPv6 = [],
                    Gateways = [],
                    Dns = [],
                    DnsSuffix = "",
                    DhcpServers = [],
                };
                int.TryParse(Sys(name, "ifindex"), NumberStyles.Integer, CultureInfo.InvariantCulture, out x.Index);
                try
                {
                    var p = ni.GetIPProperties();
                    x.IPv4 = p.UnicastAddresses.Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork).Select(Prefix).ToArray();
                    x.IPv6 = p.UnicastAddresses.Where(a => a.Address.AddressFamily == AddressFamily.InterNetworkV6).Select(Prefix).ToArray();
                    x.Gateways = p.GatewayAddresses.Select(g => g.Address.ToString()).Where(g => (g != "0.0.0.0") && (g != "::")).ToArray();
                    x.Dns = p.DnsAddresses.Select(d => d.ToString()).ToArray();
                    x.DnsSuffix = p.DnsSuffix ?? "";
                }
                catch
                {
                }
                var props = new List<KeyValuePair<String, String>>();
                var driver = "";
                try
                {
                    var link = new FileInfo(Path.Combine("/sys/class/net", name, "device", "driver")).LinkTarget;
                    if (!String.IsNullOrEmpty(link))
                        driver = Path.GetFileName(link);
                }
                catch
                {
                }
                if (driver.Length > 0)
                {
                    props.Add(P("Driver", driver));
                    x.Description = driver;
                }
                if (oper.Length > 0)
                    props.Add(P("Operational state", oper));
                var duplex = Sys(name, "duplex");
                if (duplex.Length > 0)
                    props.Add(P("Duplex", duplex));
                if (Directory.Exists(Path.Combine("/sys/class/net", name, "wireless")))
                {
                    props.Add(P("Wireless", "Yes"));
                    x.Type = "Wireless80211";
                }
                if (Directory.Exists(Path.Combine("/sys/class/net", name, "bridge")))
                    props.Add(P("Bridge", "Yes"));
                x.Properties = props.ToArray();
                res.Add(x);
            }
            return res;
        }

        public void SetAdapterEnabled(String id, bool enabled)
        {
            if (String.IsNullOrWhiteSpace(id) || id.StartsWith('-') || (id.IndexOfAny(['/', ' ', '\0']) >= 0) || !Directory.Exists(Path.Combine("/sys/class/net", id)))
                throw new ArgumentException("Unknown network interface \"" + id + "\"!");
            if (Ip == null)
                throw new Exception("The \"ip\" command (iproute2) is required!");
            UnixCommand.Change(Ip, "link", "set", "dev", id, enabled ? "up" : "down");
        }

        static String Str(JsonElement e, String name)
        {
            if (!e.TryGetProperty(name, out var v))
                return "";
            return v.ValueKind switch
            {
                JsonValueKind.String => v.GetString() ?? "",
                JsonValueKind.Number => v.GetRawText(),
                _ => "",
            };
        }

        public IReadOnlyList<OsRoute> GetRoutes()
        {
            if (Ip == null)
                throw new Exception("The \"ip\" command (iproute2) is required!");
            var res = new List<OsRoute>();
            foreach (var (fam, arg) in new[] { ("IPv4", "-4"), ("IPv6", "-6") })
            {
                String json;
                try
                {
                    json = UnixCommand.Get(Ip, "-j", arg, "route", "show", "table", "all");
                }
                catch
                {
                    continue;
                }
                if (String.IsNullOrWhiteSpace(json))
                    continue;
                using var doc = JsonDocument.Parse(json);
                foreach (var r in doc.RootElement.EnumerateArray())
                {
                    var dst = Str(r, "dst");
                    if (dst == "default")
                        dst = fam == "IPv4" ? "0.0.0.0/0" : "::/0";
                    else if ((dst.Length > 0) && (dst.IndexOf('/') < 0))
                        dst += fam == "IPv4" ? "/32" : "/128";
                    var type = Str(r, "type");
                    int.TryParse(Str(r, "metric"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var metric);
                    var proto = Str(r, "protocol");
                    if ((type.Length > 0) && (type != "unicast"))
                        proto = proto.Length > 0 ? (type + ", " + proto) : type;
                    res.Add(new OsRoute
                    {
                        Destination = dst,
                        Gateway = Str(r, "gateway"),
                        Interface = Str(r, "dev"),
                        Metric = metric,
                        Family = fam,
                        Protocol = proto,
                        Table = Str(r, "table") is var t && (t.Length > 0) ? t : "main",
                        Persistent = false,
                    });
                }
            }
            return res;
        }

        public String FlushDns()
        {
            var rc = UnixCommand.Find("resolvectl");
            if (rc != null)
            {
                UnixCommand.Change(rc, "flush-caches");
                return "The systemd-resolved cache was flushed";
            }
            var sr = UnixCommand.Find("systemd-resolve");
            if (sr != null)
            {
                UnixCommand.Change(sr, "--flush-caches");
                return "The systemd-resolved cache was flushed";
            }
            var nscd = UnixCommand.Find("nscd");
            if (nscd != null)
            {
                UnixCommand.Change(nscd, "-i", "hosts");
                return "The nscd hosts cache was invalidated";
            }
            throw new Exception("No DNS cache was found (systemd-resolved or nscd), there may be nothing to flush");
        }
    }
}
