using System;
using SysWeaver.Data;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// An allowed address, port or country of the intruder alerts, as stored in the application key value store
    /// </summary>
    public sealed class SmAllowedEntry
    {
        /// <summary>
        /// The normalized value: an address or network ("203.0.113.7", "203.0.113.0/24"), a port ("8080") or an upper case country code ("SE")
        /// </summary>
        public String Value;

        /// <summary>
        /// When it was allowed (UTC)
        /// </summary>
        public DateTime Allowed;

        /// <summary>
        /// The user name of the administrator that allowed it
        /// </summary>
        public String By;
    }

    /// <summary>
    /// An address or network that is never flagged as an intruder (a row of <see cref="ServerManagerService.AllowedIpsTable"/>)
    /// </summary>
    [TableDataPrimaryKey(nameof(Ip))]
    [TableDataDefaultOrder("-" + nameof(Allowed))]
    public sealed class SmAllowedIp
    {
        /// <summary>
        /// The address or network
        /// </summary>
        public String Ip;

        /// <summary>
        /// When it was allowed (UTC), the start of the service for the addresses of the service parameters
        /// </summary>
        [TableDataSortDesc]
        public DateTime Allowed;

        /// <summary>
        /// The user that allowed it, "Service parameters" for the addresses of the service parameters
        /// </summary>
        public String By;

        /// <summary>
        /// Remove the address from the allowed addresses
        /// </summary>
        //  The value is "-,ip,canRemove" ({2} and {3}), the addresses of the service parameters can't be removed (Remove is hidden)
        [TableDataActionsFull(new[] { TableDataActionFlags.Final },
            "Remove",
            "Remove the address from the allowed addresses (it may be flagged as an intruder again)",
            "../ServerManager/DenyIp?\"{2}\"",
            "",
            "Remove {2} from the allowed addresses?",
            "{3}",
            "{2} is no longer allowed")]
        [TableDataOrder(100)]
        public String Actions;
    }

    /// <summary>
    /// A port that is never flagged as an intruder (a row of <see cref="ServerManagerService.AllowedPortsTable"/>)
    /// </summary>
    [TableDataPrimaryKey(nameof(Port))]
    [TableDataDefaultOrder(nameof(Port))]
    public sealed class SmAllowedPort
    {
        /// <summary>
        /// The port (the local port of an incoming connection, else the remote port)
        /// </summary>
        [TableDataNumber]
        public int Port;

        /// <summary>
        /// The well known service of the port, empty if unknown
        /// </summary>
        public String Service;

        /// <summary>
        /// When it was allowed (UTC), the start of the service for the ports of the service parameters
        /// </summary>
        [TableDataSortDesc]
        public DateTime Allowed;

        /// <summary>
        /// The user that allowed it, "Service parameters" for the ports of the service parameters
        /// </summary>
        public String By;

        /// <summary>
        /// Remove the port from the allowed ports
        /// </summary>
        //  The value is "-,port,canRemove" ({2} and {3}), the ports of the service parameters can't be removed (Remove is hidden)
        [TableDataActionsFull(new[] { TableDataActionFlags.Final },
            "Remove",
            "Remove the port from the allowed ports (connections on it may be flagged as intruders again)",
            "../ServerManager/DenyPort?{2}",
            "",
            "Remove port {2} from the allowed ports?",
            "{3}",
            "Port {2} is no longer allowed")]
        [TableDataOrder(100)]
        public String Actions;
    }

    /// <summary>
    /// A country whose addresses are never flagged as intruders (a row of <see cref="ServerManagerService.AllowedCountriesTable"/>)
    /// </summary>
    [TableDataPrimaryKey(nameof(Country))]
    [TableDataDefaultOrder(nameof(Country))]
    public sealed class SmAllowedCountry
    {
        /// <summary>
        /// The two letter ISO 3166 country code
        /// </summary>
        [TableDataIsoCountry]
        public String Country;

        /// <summary>
        /// The name of the country (in English)
        /// </summary>
        public String Name;

        /// <summary>
        /// When it was allowed (UTC), the start of the service for the countries of the service parameters
        /// </summary>
        [TableDataSortDesc]
        public DateTime Allowed;

        /// <summary>
        /// The user that allowed it, "Service parameters" for the countries of the service parameters
        /// </summary>
        public String By;

        /// <summary>
        /// Remove the country from the allowed countries
        /// </summary>
        //  The value is "-,country,canRemove" ({2} and {3}), the countries of the service parameters can't be removed (Remove is hidden)
        [TableDataActionsFull(new[] { TableDataActionFlags.Final },
            "Remove",
            "Remove the country from the allowed countries (its addresses may be flagged as intruders again)",
            "../ServerManager/DenyCountry?\"{2}\"",
            "",
            "Remove {2} from the allowed countries?",
            "{3}",
            "{2} is no longer allowed")]
        [TableDataOrder(100)]
        public String Actions;
    }

    /// <summary>
    /// An intruder alert, the input of the "IntruderAlert" audit entries
    /// </summary>
    public sealed class SmIntruderAlert
    {
        /// <summary>
        /// The external address
        /// </summary>
        public String Ip;

        /// <summary>
        /// The two letter ISO 3166 country code, empty if unknown
        /// </summary>
        public String Country;

        /// <summary>
        /// The location (city, country, network)
        /// </summary>
        public String Location;

        /// <summary>
        /// The number of flagged connections (established, open long enough, not on an allowed port)
        /// </summary>
        public int Connections;

        /// <summary>
        /// The flagged ports (the local port of an incoming connection, else the remote port)
        /// </summary>
        public int[] Ports;

        /// <summary>
        /// All local ports of the flagged connections
        /// </summary>
        public String LocalPorts;

        /// <summary>
        /// All remote ports of the flagged connections
        /// </summary>
        public String RemotePorts;

        /// <summary>
        /// The processes that own the flagged connections
        /// </summary>
        public String Processes;

        /// <summary>
        /// The age of the oldest flagged connection
        /// </summary>
        public TimeSpan OldestAge;
    }

    /// <summary>
    /// A possible intruder (backdoor, trojan etc): an external address with a long established connection that isn't allowed (shown in the problems)
    /// </summary>
    public sealed class IntruderAlertException : Exception
    {
        /// <summary>
        /// Create the alert
        /// </summary>
        /// <param name="message">What was found</param>
        public IntruderAlertException(String message) : base(message)
        {
        }
    }
}
