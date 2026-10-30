using System;
using System.Collections.Generic;

namespace SysWeaver.OsServices
{
    /// <summary>
    /// A network adapter (interface) and its configuration.
    /// </summary>
    public sealed class OsNetworkAdapter
    {
        /// <summary>
        /// The id of the adapter (used by <see cref="INetworkConfig.SetAdapterEnabled"/>), ex: "{GUID}" or "eth0"
        /// </summary>
        public String Id;

        /// <summary>
        /// The name, ex: "Ethernet" or "eth0"
        /// </summary>
        public String Name;

        /// <summary>
        /// The description (hardware), ex: "Intel(R) Ethernet Connection"
        /// </summary>
        public String Description;

        /// <summary>
        /// The type, ex: "Ethernet", "Wireless80211", "Loopback"
        /// </summary>
        public String Type;

        /// <summary>
        /// The status, ex: "Up", "Down", "Disabled", "Disconnected"
        /// </summary>
        public String Status;

        /// <summary>
        /// True if the adapter is enabled (administratively up)
        /// </summary>
        public bool Enabled;

        /// <summary>
        /// True if the adapter can be enabled / disabled
        /// </summary>
        public bool CanDisable;

        /// <summary>
        /// True if the adapter is virtual (software)
        /// </summary>
        public bool Virtual;

        /// <summary>
        /// The MAC address, ex: "00-11-22-33-44-55" (empty if none)
        /// </summary>
        public String Mac;

        /// <summary>
        /// The link speed in bits per second (0 if unknown)
        /// </summary>
        public long Speed;

        /// <summary>
        /// The MTU (0 if unknown)
        /// </summary>
        public int Mtu;

        /// <summary>
        /// The IPv4 addresses with prefix lengths, ex: "192.168.1.10/24"
        /// </summary>
        public String[] IPv4;

        /// <summary>
        /// The IPv6 addresses with prefix lengths
        /// </summary>
        public String[] IPv6;

        /// <summary>
        /// The default gateways
        /// </summary>
        public String[] Gateways;

        /// <summary>
        /// The DNS servers
        /// </summary>
        public String[] Dns;

        /// <summary>
        /// The DNS suffix (empty if none)
        /// </summary>
        public String DnsSuffix;

        /// <summary>
        /// True if the IPv4 address is assigned by DHCP
        /// </summary>
        public bool Dhcp;

        /// <summary>
        /// The DHCP servers (empty if none or unknown)
        /// </summary>
        public String[] DhcpServers;

        /// <summary>
        /// The interface index (0 if unknown)
        /// </summary>
        public int Index;

        /// <summary>
        /// More details (platform specific)
        /// </summary>
        public KeyValuePair<String, String>[] Properties;
    }


    /// <summary>
    /// An entry in the routing table.
    /// </summary>
    public sealed class OsRoute
    {
        /// <summary>
        /// The destination with prefix length, ex: "0.0.0.0/0" or "192.168.1.0/24"
        /// </summary>
        public String Destination;

        /// <summary>
        /// The next hop (gateway), empty (or "0.0.0.0" / "::") for a directly connected network
        /// </summary>
        public String Gateway;

        /// <summary>
        /// The name of the interface
        /// </summary>
        public String Interface;

        /// <summary>
        /// The metric (the effective metric if known)
        /// </summary>
        public int Metric;

        /// <summary>
        /// "IPv4" or "IPv6"
        /// </summary>
        public String Family;

        /// <summary>
        /// Where the route comes from, ex: "Local", "Static", "DHCP", "kernel", "boot"
        /// </summary>
        public String Protocol;

        /// <summary>
        /// The routing table (Linux, ex: "main", "local"), empty on Windows
        /// </summary>
        public String Table;

        /// <summary>
        /// True if the route is persistent (Windows) or permanent
        /// </summary>
        public bool Persistent;
    }


    /// <summary>
    /// Reads and changes the network configuration: the adapters, the routing table, the DNS cache and the hosts file.
    /// Get an instance using <see cref="OsSystem.NetworkConfig"/> (created by <see cref="IServiceHostFactory.CreateNetworkConfig"/>).
    /// Changes requires elevation (admin / root), failures are reported by throwing an exception (with a description).
    /// </summary>
    public interface INetworkConfig
    {
        /// <summary>
        /// The display name of the implementation
        /// </summary>
        String Name { get; }

        /// <summary>
        /// The full path of the hosts file
        /// </summary>
        String HostsFile { get; }

        /// <summary>
        /// Get all network adapters (including disabled ones, if possible).
        /// </summary>
        /// <returns>The adapters</returns>
        IReadOnlyList<OsNetworkAdapter> GetAdapters();

        /// <summary>
        /// Enable or disable a network adapter.
        /// </summary>
        /// <param name="id">The id of the adapter</param>
        /// <param name="enabled">True to enable, false to disable</param>
        void SetAdapterEnabled(String id, bool enabled);

        /// <summary>
        /// Get the routing table (IPv4 and IPv6).
        /// </summary>
        /// <returns>The routes</returns>
        IReadOnlyList<OsRoute> GetRoutes();

        /// <summary>
        /// Flush the DNS resolver cache.
        /// </summary>
        /// <returns>A description of what was done</returns>
        String FlushDns();
    }
}
