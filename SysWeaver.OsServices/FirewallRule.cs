using System;
using System.Collections.Generic;

namespace SysWeaver.OsServices
{
    /// <summary>
    /// The direction of the traffic that a firewall rule applies to.
    /// </summary>
    public enum FirewallDirections
    {
        /// <summary>
        /// Incoming traffic.
        /// </summary>
        In,
        /// <summary>
        /// Outgoing traffic.
        /// </summary>
        Out,
        /// <summary>
        /// Forwarded (routed) traffic (Linux only).
        /// </summary>
        Forward,
    }

    /// <summary>
    /// What a firewall rule does with the traffic that it matches.
    /// </summary>
    public enum FirewallActions
    {
        /// <summary>
        /// Allow the traffic.
        /// </summary>
        Allow,
        /// <summary>
        /// Block (drop) the traffic.
        /// </summary>
        Block,
        /// <summary>
        /// Reject the traffic (the sender is told that it was rejected, Linux only).
        /// </summary>
        Reject,
        /// <summary>
        /// Allow, but limit the number of connections (ufw only).
        /// </summary>
        Limit,
    }


    /// <summary>
    /// A firewall rule.
    /// String values that can contain multiple values uses a comma separated list, an empty string or "*" means any.
    /// </summary>
    public sealed class FirewallRule
    {
        /// <summary>
        /// The id of the rule (assigned by the firewall, ignored when adding a rule).
        /// NOTE: The id may change when the rule is updated.
        /// </summary>
        public String Id;

        /// <summary>
        /// The name of the rule, on Windows this may be an indirect string, ex: "@FirewallAPI.dll,-28502".
        /// </summary>
        public String Name;

        /// <summary>
        /// The name to display (indirect strings are resolved), ignored when adding / updating a rule.
        /// </summary>
        public String DisplayName;

        /// <summary>
        /// The description of the rule (resolved if it's an indirect string).
        /// </summary>
        public String Description;

        /// <summary>
        /// The group that the rule belongs to (resolved if it's an indirect string), empty if none.
        /// </summary>
        public String Group;

        /// <summary>
        /// True if the rule is enabled.
        /// </summary>
        public bool Enabled = true;

        /// <summary>
        /// The direction of the traffic.
        /// </summary>
        public FirewallDirections Direction;

        /// <summary>
        /// What to do with matching traffic.
        /// </summary>
        public FirewallActions Action;

        /// <summary>
        /// The protocol: "Any", "TCP", "UDP", "ICMPv4", "ICMPv6" or a protocol number.
        /// </summary>
        public String Protocol = "Any";

        /// <summary>
        /// The local ports, ex: "80,443,8000-8100" (empty or "*" = any).
        /// </summary>
        public String LocalPorts;

        /// <summary>
        /// The remote ports, ex: "80,443" (empty or "*" = any).
        /// </summary>
        public String RemotePorts;

        /// <summary>
        /// The local addresses, ex: "192.168.1.10,10.0.0.0/8" (empty or "*" = any).
        /// </summary>
        public String LocalAddresses;

        /// <summary>
        /// The remote addresses, ex: "192.168.1.0/24" or "LocalSubnet" (empty or "*" = any).
        /// </summary>
        public String RemoteAddresses;

        /// <summary>
        /// The full name of the program that the rule applies to (empty = any).
        /// </summary>
        public String Program;

        /// <summary>
        /// The name of the service that the rule applies to (empty = any, Windows only).
        /// </summary>
        public String Service;

        /// <summary>
        /// The network interface (or interface types on Windows: "All", "Lan", "Wireless", "RemoteAccess") that the rule applies to (empty = any).
        /// </summary>
        public String Interface;

        /// <summary>
        /// The profiles that the rule applies to, a comma separated list of "Domain", "Private" and "Public", or "All" (Windows only).
        /// </summary>
        public String Profiles = "All";

        /// <summary>
        /// The ICMP types and codes, ex: "8:*" (Windows only).
        /// </summary>
        public String IcmpTypes;

        /// <summary>
        /// True if edge traversal is allowed (Windows only).
        /// </summary>
        public bool EdgeTraversal;

        /// <summary>
        /// True if the rule can't be changed (ex: defined by a group policy or a system rule).
        /// </summary>
        public bool ReadOnly;

        /// <summary>
        /// OS specific details, ex: the native rule specification.
        /// </summary>
        public KeyValuePair<String, String>[] Properties;

        /// <summary>
        /// Create a copy (shallow).
        /// </summary>
        /// <returns>The copy</returns>
        public FirewallRule Clone() => (FirewallRule)MemberwiseClone();

        public override string ToString() => String.Concat(DisplayName ?? Name, " (", Direction, " ", Action, ")");
    }


    /// <summary>
    /// The state of a firewall profile (Windows: Domain, Private, Public, Linux: a single profile).
    /// </summary>
    public sealed class FirewallProfile
    {
        /// <summary>
        /// The name of the profile, ex: "Private".
        /// </summary>
        public String Name;

        /// <summary>
        /// True if the firewall is enabled for this profile.
        /// </summary>
        public bool Enabled;

        /// <summary>
        /// True if the profile is currently active (used by a network).
        /// </summary>
        public bool Active;

        /// <summary>
        /// What happens to incoming traffic that doesn't match any rule.
        /// </summary>
        public FirewallActions DefaultInbound;

        /// <summary>
        /// What happens to outgoing traffic that doesn't match any rule.
        /// </summary>
        public FirewallActions DefaultOutbound;
    }
}
