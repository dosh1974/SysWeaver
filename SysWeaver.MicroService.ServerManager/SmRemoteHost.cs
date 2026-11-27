using System;
using SysWeaver.Data;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// The established connections to one external ip address (a row of <see cref="ServerManagerService.RemoteHostsTable"/>):
    /// local and LAN addresses are left out, one row per address
    /// </summary>
    [TableDataPrimaryKey(nameof(Address))]
    [TableDataDefaultOrder("-" + nameof(TotalAge))]
    public sealed class SmRemoteHost
    {
        /// <summary>
        /// The remote (external) address
        /// </summary>
        [TableDataOrder(1)]
        [TableDataIp]
        public String Address;

        /// <summary>
        /// The number of established connections to the address
        /// </summary>
        [TableDataOrder(2)]
        [TableDataNumber]
        public int Connections;

        /// <summary>
        /// "Incoming", "Outgoing" or "Both"
        /// </summary>
        [TableDataOrder(3)]
        public String Direction;

        /// <summary>
        /// All local ports of the connections (sorted, comma separated)
        /// </summary>
        [TableDataOrder(4)]
        [TableDataText(32)]
        public String LocalPorts;

        /// <summary>
        /// All remote ports of the connections (sorted, comma separated)
        /// </summary>
        [TableDataOrder(5)]
        [TableDataText(32)]
        public String RemotePorts;

        /// <summary>
        /// The processes that own the connections, ex: "svchost (#1234), chrome (#88)"
        /// </summary>
        [TableDataOrder(6)]
        [TableDataText(40)]
        public String Processes;

        /// <summary>
        /// The accumulated age of all connections to the address
        /// </summary>
        [TableDataOrder(7)]
        [TableDataDuration]
        [TableDataSortDesc]
        public TimeSpan TotalAge;

        /// <summary>
        /// The age of the oldest connection to the address
        /// </summary>
        [TableDataOrder(8)]
        [TableDataDuration]
        public TimeSpan OldestAge;

        /// <summary>
        /// When the oldest connection was created (UTC), or first seen if the OS doesn't tell
        /// </summary>
        [TableDataOrder(9)]
        [TableDataHide]
        public DateTime Since;

        /// <summary>
        /// True if the address is flagged as a possible intruder (see the problems)
        /// </summary>
        [TableDataOrder(10)]
        public bool Flagged;

        /// <summary>
        /// Allow the address (only shown while it is flagged): connections with it are never flagged again (requires Admin)
        /// </summary>
        //  The value is "-,address,flagged" ({2} and {3}): Allow is only shown on flagged rows.
        //  Not final: the row stays (the connections are still there), the table re-reads it and Flagged turns false.
        [TableDataActionsFull(
            "Allow",
            "Allow the address: connections with it are never flagged as an intruder",
            "../ServerManager/AllowIp?\"{2}\"",
            "",
            "Allow {2}?\nConnections with it will never be flagged as an intruder. Remove it again on the Allowed addresses page.",
            "{3}",
            "{2} is allowed")]
        [TableDataOrder(11)]
        public String Actions => String.Concat("-,", Address, Flagged ? ",1" : ",");

        /// <summary>
        /// The location of the address (empty if unknown, or when no ip location service is available)
        /// </summary>
        [TableDataExpand("{0}", "")]
        public SmIpInfo Ip;
    }
}
