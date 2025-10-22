using System;

namespace SysWeaver.Security
{
    public interface IFirewallHandler
    {
        /// <summary>
        /// Add a firewall rule
        /// </summary>
        /// <param name="ruleName">Name of the rule (unique id)</param>
        /// <param name="port">The port to open</param>
        /// <param name="msg">Message handler</param>
        /// <param name="messagePrefix">Message prefix</param>
        /// <param name="protocol">The protcol to open up traffic for</param>
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



    public enum FirewallProtcols
    {
        Tcp = 0,
        Udp,
        TcpAndUdp,
    }

    public enum FirewallDirections
    {
        Inbound = 0,
        Outbound,
    }

}
