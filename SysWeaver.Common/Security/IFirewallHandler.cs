using System;

namespace SysWeaver.Security
{
    /// <summary>
    /// Represents an object that can add and remove firewall rules (used by the http servers to open the ports they listen on).
    /// </summary>
    public interface IFirewallHandler
    {
        /// <summary>
        /// Add a firewall rule
        /// </summary>
        /// <param name="ruleName">Name of the rule (unique id)</param>
        /// <param name="port">The port to open</param>
        /// <param name="msg">Message handler</param>
        /// <param name="messagePrefix">Message prefix</param>
        /// <param name="protocol">The protocol to open up traffic for</param>
        /// <param name="direction">The direction of traffic to open up</param>
        /// <returns>True if the rule was successfully added or changed</returns>
        bool AddOrSet(String ruleName, int port, IMessageHost msg = null, String messagePrefix = null, FirewallProtcols protocol = FirewallProtcols.Tcp, FirewallDirections direction = FirewallDirections.Inbound);

        /// <summary>
        /// Remove a firewall rule
        /// </summary>
        /// <param name="ruleName">Name of the rule (unique id)</param>
        /// <param name="msg">Message handler</param>
        /// <param name="messagePrefix">Message prefix</param>
        /// <returns>True if the rule was found and removed or if the rule doesn't exist, else false</returns>
        bool Remove(String ruleName, IMessageHost msg = null, String messagePrefix = null);
    }



    /// <summary>
    /// The protocol(s) that a firewall rule applies to
    /// </summary>
    public enum FirewallProtcols
    {
        /// <summary>
        /// TCP traffic
        /// </summary>
        Tcp = 0,
        /// <summary>
        /// UDP traffic
        /// </summary>
        Udp,
        /// <summary>
        /// Both TCP and UDP traffic
        /// </summary>
        TcpAndUdp,
    }

    /// <summary>
    /// The direction of traffic that a firewall rule applies to
    /// </summary>
    public enum FirewallDirections
    {
        /// <summary>
        /// Incoming traffic
        /// </summary>
        Inbound = 0,
        /// <summary>
        /// Outgoing traffic
        /// </summary>
        Outbound,
    }

}
