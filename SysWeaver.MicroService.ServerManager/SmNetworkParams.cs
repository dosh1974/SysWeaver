using System;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Parameters of the network monitoring of the <see cref="ServerManagerService"/> (<see cref="ServerManagerParams.Network"/>):
    /// the connection tables and the intruder alerts.
    /// </summary>
    /// <remarks>
    /// The intruder alerts need an ip location service (SysWeaver.Net.IIpLocation, ex: SysWeaver.IpLocation.IpLocationService) to find the country of an address.
    /// An external address (not local or on a local network) that has an established connection for at least <see cref="IntruderMinSeconds"/>
    /// is flagged as a possible intruder (backdoor, trojan etc) unless the address, the country of the address or the port of the connection is allowed.
    /// The port of a connection is the local port for an incoming connection (the local port is listening), else the remote port.
    /// The allowed addresses, ports and countries are the ones in these parameters, plus the ones allowed by an administrator (stored in the application key value store).
    /// </remarks>
    public sealed class SmNetworkParams
    {
        /// <summary>
        /// Set to false to disable the network monitoring (the connection tables are empty and there are no intruder alerts)
        /// </summary>
        public bool Enabled = true;

        /// <summary>
        /// How often the connections are read, in seconds
        /// </summary>
        public int UpdateSeconds = 5;

        /// <summary>
        /// True to include the bound UDP ports in the table of all connections
        /// </summary>
        public bool IncludeUdp = true;

        /// <summary>
        /// Set to false to disable the intruder alerts (they also need an ip location service)
        /// </summary>
        public bool IntruderAlerts = true;

        /// <summary>
        /// How often the external connections are checked for intruders, in seconds
        /// </summary>
        public int IntruderCheckSeconds = 10;

        /// <summary>
        /// An established connection must have been open for at least this many seconds to be flagged as an intruder
        /// </summary>
        public int IntruderMinSeconds = 30;

        /// <summary>
        /// A flagged address that is still connected is reported again (in the log and the audit log) after this many minutes
        /// </summary>
        public int IntruderRepeatMinutes = 60;

        /// <summary>
        /// Connections on these ports are never flagged (the local port of an incoming connection, else the remote port).
        /// More ports can be allowed by an administrator ("Allow port" on the problems page).
        /// </summary>
        public int[] AllowedPorts = [80, 443];

        /// <summary>
        /// Addresses (ex: "203.0.113.7") and networks (ex: "203.0.113.0/24", "2001:db8::/32") that are never flagged.
        /// More addresses can be allowed by an administrator ("Allow IP" on the problems page).
        /// </summary>
        public String[] AllowedIps;

        /// <summary>
        /// Countries (two letter ISO 3166 codes, ex: "SE") whose addresses are never flagged.
        /// More countries can be allowed by an administrator ("Allow country" on the problems page).
        /// </summary>
        public String[] AllowedCountries;

        /// <summary>
        /// The maximum number of ip location lookups running at the same time
        /// </summary>
        public int MaxLocationLookups = 4;

        /// <summary>
        /// How long the location of an address is kept, in minutes (an unknown location is retried after a tenth of this)
        /// </summary>
        public int LocationCacheMinutes = 60;
    }
}
