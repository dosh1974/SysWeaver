using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Data;
using SysWeaver.Net;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Network monitoring: all connections of the server (like netstat), the established connections to external addresses (one row per address),
    /// and intruder alerts for long established connections to addresses that aren't allowed (see <see cref="SmNetworkParams"/>).
    /// </summary>
    //  The allow lists: one page (allowed.html, k = ip, port or country) with an add box (Admin) above the live table
    [WebMenuEmbedded(null, "Admin/AllowedIps", "Allowed addresses", "ServerManager/allowed.html?k=ip", "The addresses that are never flagged as intruders (see the problems)", "icons/table_services.svg", -1, Roles.AdminOps)]
    [WebMenuEmbedded(null, "Admin/AllowedPorts", "Allowed ports", "ServerManager/allowed.html?k=port", "The ports whose connections are never flagged as intruders (see the problems)", "icons/table_services.svg", -1, Roles.AdminOps)]
    [WebMenuEmbedded(null, "Admin/AllowedCountries", "Allowed countries", "ServerManager/allowed.html?k=country", "The countries whose addresses are never flagged as intruders (see the problems)", "icons/table_services.svg", -1, Roles.AdminOps)]
    public sealed partial class ServerManagerService : IHaveProblemActions
    {
        #region State

        SmNetworkParams NetP;

        /// <summary>
        /// Reads the connections (every <see cref="SmNetworkParams.UpdateSeconds"/>), null if the monitoring is disabled
        /// </summary>
        PeriodicTask NetTask;

        /// <summary>
        /// Checks for intruders (every <see cref="SmNetworkParams.IntruderCheckSeconds"/>), null if disabled
        /// </summary>
        PeriodicTask IntruderTask;

        /// <summary>
        /// Failures of the network monitoring
        /// </summary>
        readonly ExceptionTracker NetEx = new ExceptionTracker();

        /// <summary>
        /// The latest connections (all, and the established external ones per address)
        /// </summary>
        sealed class NetSnapshot
        {
            public SmNetConnection[] All = [];
            public SmRemoteHost[] Hosts = [];

            /// <summary>
            /// The established connections to external addresses, the key is the address
            /// </summary>
            public Dictionary<String, List<NetConn>> External = new(StringComparer.Ordinal);
        }

        /// <summary>
        /// An established connection to an external address (used by the intruder check)
        /// </summary>
        sealed class NetConn
        {
            public SmNetConnection Row;
            public bool Incoming;

            /// <summary>
            /// The port of the connection: the local port of an incoming connection, else the remote port
            /// </summary>
            public int ServicePort => Incoming ? Row.LocalPort : Row.RemotePort;
        }

        volatile NetSnapshot NetCurrent = new NetSnapshot();

        /// <summary>
        /// When a connection was first seen (Environment ticks are not used, the time is shown), the key is "protocol|local|remote"
        /// </summary>
        readonly Dictionary<String, DateTime> NetFirstSeen = new(StringComparer.Ordinal);

        /// <summary>
        /// The names of processes that aren't in <see cref="Processes"/> (cleared when the connections are read)
        /// </summary>
        readonly Dictionary<int, String> NetProcessNames = new();

        #endregion//State

        #region Ip location

        /// <summary>
        /// The ip location service, found when the service starts (or later, the service manager is asked again at most twice a minute), null if none is available
        /// </summary>
        volatile IIpLocation NetLocator;

        long NetLocatorRetry;

        IIpLocation GetLocator()
        {
            var l = NetLocator;
            if (l != null)
                return l;
            var now = Environment.TickCount64;
            if (now < Interlocked.Read(ref NetLocatorRetry))
                return null;
            Interlocked.Exchange(ref NetLocatorRetry, now + 30_000);
            l = Manager.TryGet<IIpLocation>();
            NetLocator = l;
            return l;
        }

        sealed class CachedIpInfo
        {
            public SmIpInfo Info;
            public long Expires;
            public Task<SmIpInfo> Lookup;
        }

        /// <summary>
        /// The locations of external addresses, the key is the address
        /// </summary>
        readonly ConcurrentDictionary<String, CachedIpInfo> IpInfos = new(StringComparer.Ordinal);

        SemaphoreSlim IpLookupLimit;

        const int MaxCachedIpInfos = 10_000;

        /// <summary>
        /// Get the location of an external address if it's known, else start a lookup (the next snapshot has it)
        /// </summary>
        /// <param name="ip">The address</param>
        /// <returns>The location, null if it isn't known (yet)</returns>
        SmIpInfo TryGetIpInfo(String ip)
        {
            if (IpInfos.TryGetValue(ip, out var c) && (c.Info != null) && (Environment.TickCount64 < c.Expires))
                return c.Info;
            _ = GetIpInfo(ip);
            return c?.Info;
        }

        /// <summary>
        /// Get the location of an external address (looked up at most once at a time per address, at most <see cref="SmNetworkParams.MaxLocationLookups"/> at the same time)
        /// </summary>
        /// <param name="ip">The address</param>
        /// <returns>The location, <see cref="SmIpInfo.Unknown"/> if unknown, or if no ip location service is available</returns>
        Task<SmIpInfo> GetIpInfo(String ip)
        {
            var now = Environment.TickCount64;
            if (IpInfos.TryGetValue(ip, out var c))
            {
                if ((c.Info != null) && (now < c.Expires))
                    return Task.FromResult(c.Info);
                var running = c.Lookup;
                if ((running != null) && !running.IsCompleted)
                    return running;
            }
            var locator = GetLocator();
            if (locator == null)
                return Task.FromResult(SmIpInfo.Unknown);
            if (IpInfos.Count >= MaxCachedIpInfos)
                foreach (var x in IpInfos)
                    if ((x.Value.Lookup == null) && (now >= x.Value.Expires))
                        IpInfos.TryRemove(x);
            var n = new CachedIpInfo { Info = c?.Info, Expires = c?.Expires ?? 0 };
            var t = Lookup(locator, ip, n);
            n.Lookup = t;
            IpInfos[ip] = n;
            return t;
        }

        async Task<SmIpInfo> Lookup(IIpLocation locator, String ip, CachedIpInfo into)
        {
            var limit = IpLookupLimit;
            var cacheMs = Math.Max(1, NetP?.LocationCacheMinutes ?? 60) * 60_000L;
            SmIpInfo info = null;
            await limit.WaitAsync().ConfigureAwait(false);
            try
            {
                var loc = await locator.GetLocation(ip).WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
                info = SmIpInfo.From(loc);
            }
            catch (TimeoutException)
            {
            }
            catch (Exception ex)
            {
                NetEx.OnException(ex);
            }
            finally
            {
                limit.Release();
            }
            var known = (info != null) && (info != SmIpInfo.Unknown);
            into.Info = info ?? into.Info ?? SmIpInfo.Unknown;
            into.Expires = Environment.TickCount64 + (known ? cacheMs : (cacheMs / 10));
            into.Lookup = null;
            return into.Info;
        }

        #endregion//Ip location

        #region Init

        /// <summary>
        /// Start the network monitoring (called by the constructor)
        /// </summary>
        void InitNetwork(SmNetworkParams p)
        {
            p = p ?? new SmNetworkParams();
            NetP = p;
            IpLookupLimit = new SemaphoreSlim(Math.Max(1, p.MaxLocationLookups));
            NetLocator = Manager.TryGet<IIpLocation>();
            NetLocatorRetry = Environment.TickCount64 + 30_000;
            AllowedIps = new SmAllowList(AllowedIpsKey, p.AllowedIps, NormalizeIp);
            AllowedPorts = new SmAllowList(AllowedPortsKey, p.AllowedPorts?.Select(x => x.ToString(CultureInfo.InvariantCulture)), NormalizePort);
            AllowedCountries = new SmAllowList(AllowedCountriesKey, p.AllowedCountries, NormalizeCountry);
            foreach (var e in AllowedIps.InvalidConfig.Concat(AllowedPorts.InvalidConfig).Concat(AllowedCountries.InvalidConfig))
                Manager.AddMessage(String.Concat(LogPrefix, "Invalid allowed value \"", e, "\" in the network parameters, ignored"), MessageLevels.Warning);
            if (!p.Enabled)
                return;
            NetTask = new PeriodicTask(UpdateNetwork, Math.Max(1, p.UpdateSeconds) * 1000);
            if (p.IntruderAlerts)
                //  Started now, the first check after one interval (the first connection snapshot is made by then)
                IntruderTask = new PeriodicTask(CheckIntruders, Math.Max(1, p.IntruderCheckSeconds) * 1000, runNow: true, continueOnException: true, initialDelay: true);
        }

        /// <summary>
        /// Stop the network monitoring (called by Dispose)
        /// </summary>
        void DisposeNetwork()
        {
            Interlocked.Exchange(ref IntruderTask, null)?.Dispose();
            Interlocked.Exchange(ref NetTask, null)?.Dispose();
        }

        /// <summary>
        /// The statistics of the network monitoring: the failures, and one exception tracker per flagged address (shown in the problems)
        /// </summary>
        IEnumerable<Stats> GetNetworkStats(String sys)
        {
            foreach (var x in NetEx.GetStats(sys, "NetworkExs."))
                yield return x;
            yield return new Stats(sys, "Network.Connections", NetCurrent.All.Length, "The number of network connections (and listeners)");
            yield return new Stats(sys, "Network.ExternalHosts", NetCurrent.Hosts.Length, "The number of external addresses with established connections");
            yield return new Stats(sys, "Network.Intruders", Intruders.Count, "The number of addresses flagged as possible intruders");
            yield return new Stats(sys, "Network.HaveIpLocation", NetLocator != null, "True if an ip location service is available (needed for the intruder alerts)");
            yield return new Stats(sys, "Network.PlatformSupport", NetConnections.HavePlatformSupport, "True if the connections are read with the owning process (else the .NET network information is used)");
            foreach (var i in Intruders.Values)
                foreach (var x in i.Tracker.GetStats(sys, String.Concat(IntruderSourcePrefix, i.Ip, ".")))
                    yield return x;
        }

        #endregion//Init

        #region Connections

        static String KeyOf(NetConnection c)
            => String.Concat(c.Protocol == NetConnectionProtocols.Tcp ? "T|" : "U|", c.LocalAddress?.ToString(), "|", c.LocalPort.ToString(CultureInfo.InvariantCulture), "|", c.RemoteAddress?.ToString(), "|", c.RemotePort.ToString(CultureInfo.InvariantCulture));

        String ProcessNameOf(int pid)
        {
            if (pid <= 0)
                return "";
            if (Processes.TryGetValue(pid, out var p))
                return p.Name ?? "";
            var names = NetProcessNames;
            if (names.TryGetValue(pid, out var n))
                return n;
            try
            {
                using var pr = System.Diagnostics.Process.GetProcessById(pid);
                n = pr.ProcessName;
            }
            catch
            {
                n = "";
            }
            names[pid] = n;
            return n;
        }

        /// <summary>
        /// Read the connections and build the tables (periodic)
        /// </summary>
        bool UpdateNetwork()
        {
            using var _ = PerfMon.Track(nameof(UpdateNetwork));
            try
            {
                var p = NetP;
                var list = NetConnections.Get(p.IncludeUdp);
                var now = DateTime.UtcNow;
                var listening = new HashSet<int>();
                foreach (var c in list)
                    if ((c.Protocol == NetConnectionProtocols.Tcp) && (c.State == NetConnectionStates.Listen))
                        listening.Add(c.LocalPort);
                NetProcessNames.Clear();
                var first = NetFirstSeen;
                var seen = new HashSet<String>(StringComparer.Ordinal);
                var all = new SmNetConnection[list.Count];
                var snap = new NetSnapshot();
                var ext = snap.External;
                for (int i = 0; i < all.Length; ++i)
                {
                    var c = list[i];
                    var key = KeyOf(c);
                    seen.Add(key);
                    if (!first.TryGetValue(key, out var since))
                    {
                        since = now;
                        first[key] = since;
                    }
                    if (c.Created != default)
                        since = c.Created;
                    var tcp = c.Protocol == NetConnectionProtocols.Tcp;
                    var isListen = tcp && (c.State == NetConnectionStates.Listen);
                    var ra = c.RemoteAddress;
                    var haveRemote = (ra != null) && !isListen && tcp;
                    var incoming = haveRemote && listening.Contains(c.LocalPort);
                    var external = haveRemote && !NetConnections.IsLocalOrLan(ra);
                    var raText = haveRemote ? ra.ToString() : "";
                    var row = new SmNetConnection
                    {
                        Protocol = tcp ? "TCP" : "UDP",
                        State = tcp ? c.State.ToString() : "",
                        Direction = !tcp ? "Bound" : (isListen ? "Listening" : (incoming ? "Incoming" : "Outgoing")),
                        LocalAddress = c.LocalAddress?.ToString() ?? "",
                        LocalPort = c.LocalPort,
                        RemoteAddress = raText,
                        RemotePort = haveRemote ? c.RemotePort : 0,
                        Remote = !haveRemote ? "" : (external ? "Internet" : (IPAddress.IsLoopback(ra) ? "Local" : "LAN")),
                        ProcessId = c.ProcessId,
                        ProcessName = ProcessNameOf(c.ProcessId),
                        Age = since <= now ? (now - since) : TimeSpan.Zero,
                        Since = since,
                        Ip = external ? TryGetIpInfo(raText) : null,
                    };
                    all[i] = row;
                    if (external && (c.State == NetConnectionStates.Established))
                    {
                        if (!ext.TryGetValue(raText, out var l))
                        {
                            l = new List<NetConn>();
                            ext.Add(raText, l);
                        }
                        l.Add(new NetConn { Row = row, Incoming = incoming });
                    }
                }
                //  Forget the connections that are gone
                if (first.Count > seen.Count)
                    foreach (var k in first.Keys.Where(x => !seen.Contains(x)).ToList())
                        first.Remove(k);
                snap.All = all;
                snap.Hosts = ext.Select(x => CreateHost(x.Key, x.Value)).ToArray();
                NetCurrent = snap;
            }
            catch (Exception ex)
            {
                NetEx.OnException(ex);
            }
            return true;
        }

        static String JoinPorts(IEnumerable<int> ports) => String.Join(", ", ports.Distinct().Order().Select(x => x.ToString(CultureInfo.InvariantCulture)));

        static String JoinProcesses(IEnumerable<SmNetConnection> rows)
            => String.Join(", ", rows.Where(x => x.ProcessId > 0).Select(x => x.ProcessId).Distinct().Order()
                .Select(pid => String.Concat(rows.First(r => r.ProcessId == pid).ProcessName is { Length: > 0 } n ? n : "?", " (#", pid.ToString(CultureInfo.InvariantCulture), ")")));

        SmRemoteHost CreateHost(String ip, List<NetConn> conns)
        {
            var rows = conns.Select(x => x.Row).ToList();
            var anyIn = conns.Any(x => x.Incoming);
            var anyOut = conns.Any(x => !x.Incoming);
            var oldest = rows.MaxBy(x => x.Age);
            return new SmRemoteHost
            {
                Address = ip,
                Connections = rows.Count,
                Direction = anyIn ? (anyOut ? "Both" : "Incoming") : "Outgoing",
                LocalPorts = JoinPorts(rows.Select(x => x.LocalPort)),
                RemotePorts = JoinPorts(rows.Select(x => x.RemotePort)),
                Processes = JoinProcesses(rows),
                TotalAge = TimeSpan.FromTicks(rows.Sum(x => x.Age.Ticks)),
                OldestAge = oldest?.Age ?? TimeSpan.Zero,
                Since = oldest?.Since ?? default,
                Flagged = Intruders.ContainsKey(ip),
                Ip = rows[0].Ip,
            };
        }

        /// <summary>
        /// All network connections of the server, like netstat: TCP connections and listeners and the bound UDP ports, with the owning process.
        /// Connections to external addresses (not local or on a local network) have the location of the address, if an ip location service is available.
        /// </summary>
        /// <param name="r">Table request: paging, sorting and filtering</param>
        /// <returns>Table data, one row per connection</returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        [WebMenuTable(null, "NetworkConnections", "Network connections", "All network connections of the server (like netstat), with the owning process and the location of external addresses", "icons/table_services.svg", -3)]
        [WebApiClientCache(4)]
        [WebApiRequestCache(3)]
        public TableData NetworkConnectionsTable(TableDataRequest r)
            => WithoutIpColumns(TableDataTools.Get(r, 5000, NetCurrent.All));

        /// <summary>
        /// The names of the columns of <see cref="SmIpInfo"/> (expanded from the "Ip" member of the rows: "IpCountry", "IpCity" ...)
        /// </summary>
        static readonly HashSet<String> IpColumnNames = typeof(SmIpInfo).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).Select(x => x.Name)
            .Concat(typeof(SmIpInfo).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).Select(x => x.Name))
            .Select(x => "Ip" + x).ToHashSet(StringComparer.Ordinal);

        /// <summary>
        /// Remove the location columns (<see cref="SmIpInfo"/>) if there is no ip location service (they would always be empty)
        /// </summary>
        /// <param name="data">The table data (modified in place)</param>
        /// <returns>The table data</returns>
        TableData WithoutIpColumns(TableData data)
        {
            if ((data == null) || (GetLocator() != null))
                return data;
            var cols = data.Cols;
            if (cols == null)
                return data;
            for (int i = cols.Length - 1; i >= 0; --i)
                if (IpColumnNames.Contains(cols[i].Name))
                    data.RemoveColumnIndex(i);
            return data;
        }

        /// <summary>
        /// The established connections to external addresses (local and LAN addresses are left out), one row per address:
        /// the number of connections, all local and remote ports, the processes and the accumulated age of the connections.
        /// </summary>
        /// <param name="r">Table request: paging, sorting and filtering</param>
        /// <returns>Table data, one row per external address</returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        [WebMenuTable(null, "RemoteHosts", "External connections", "The established connections to external addresses, one row per address (local and LAN addresses are left out)", "icons/table_services.svg", -2)]
        [WebApiClientCache(4)]
        [WebApiRequestCache(3)]
        public TableData RemoteHostsTable(TableDataRequest r)
        {
            var hosts = NetCurrent.Hosts;
            //  Flagged is refreshed here, so an address allowed with the row action (or on the problems page) isn't flagged until the next snapshot
            foreach (var h in hosts)
                h.Flagged = Intruders.ContainsKey(h.Address);
            return WithoutIpColumns(TableDataTools.Get(r, 5000, hosts));
        }

        #endregion//Connections

        #region Intruders

        /// <summary>
        /// The prefix of the problem source of an intruder alert (followed by the address)
        /// </summary>
        const String IntruderSourcePrefix = "Intruder alert ";

        sealed class Intruder
        {
            public String Ip;
            public readonly ExceptionTracker Tracker = new ExceptionTracker();
            public SmIntruderAlert Alert;
            public long LastReportTicks;
        }

        /// <summary>
        /// The flagged addresses, the key is the address
        /// </summary>
        readonly ConcurrentDictionary<String, Intruder> Intruders = new(StringComparer.Ordinal);

        static readonly HttpApiAudit IntruderAudit = new HttpApiAudit("ServerManager/IntruderAlert", AuditGroup);

        /// <summary>
        /// Check the established external connections for intruders (periodic, only if an ip location service is available)
        /// </summary>
        async Task<bool> CheckIntruders()
        {
            using var _ = PerfMon.Track(nameof(CheckIntruders));
            try
            {
                if (GetLocator() == null)
                    return true;
                var p = NetP;
                var minAge = TimeSpan.FromSeconds(Math.Max(0, p.IntruderMinSeconds));
                var repeat = TimeSpan.FromMinutes(Math.Max(1, p.IntruderRepeatMinutes)).Ticks;
                foreach (var (ip, conns) in NetCurrent.External)
                {
                    if (AllowedIps.Contains(ip))
                        continue;
                    var flagged = conns.Where(x => (x.Row.Age >= minAge) && !AllowedPorts.Contains(x.ServicePort.ToString(CultureInfo.InvariantCulture))).ToList();
                    if (flagged.Count == 0)
                        continue;
                    var info = await GetIpInfo(ip).ConfigureAwait(false);
                    if ((info.Country.Length > 0) && AllowedCountries.Contains(info.Country))
                        continue;
                    var rows = flagged.Select(x => x.Row).ToList();
                    var alert = new SmIntruderAlert
                    {
                        Ip = ip,
                        Country = info.Country,
                        Location = info.ToString(),
                        Connections = rows.Count,
                        Ports = flagged.Select(x => x.ServicePort).Distinct().Order().ToArray(),
                        LocalPorts = JoinPorts(rows.Select(x => x.LocalPort)),
                        RemotePorts = JoinPorts(rows.Select(x => x.RemotePort)),
                        Processes = JoinProcesses(rows),
                        OldestAge = rows.Max(x => x.Age),
                    };
                    var i = Intruders.GetOrAdd(ip, x => new Intruder { Ip = x });
                    i.Alert = alert;
                    var now = DateTime.UtcNow.Ticks;
                    if ((i.LastReportTicks != 0) && ((now - i.LastReportTicks) < repeat))
                        continue;
                    i.LastReportTicks = now;
                    Report(i, alert);
                }
            }
            catch (Exception ex)
            {
                NetEx.OnException(ex);
            }
            return true;
        }

        static String AlertText(SmIntruderAlert a)
        {
            var sb = new StringBuilder();
            sb.Append("Possible intruder (backdoor, trojan etc): ").Append(a.Ip);
            if (!String.IsNullOrEmpty(a.Location))
                sb.Append(" (").Append(a.Location).Append(')');
            else
                sb.Append(" (unknown location)");
            sb.Append(" has ").Append(a.Connections).Append(a.Connections == 1 ? " established connection" : " established connections");
            sb.Append(" for ").Append(FormatAge(a.OldestAge));
            sb.Append(" on ").Append(a.Ports.Length == 1 ? "port " : "ports ").Append(String.Join(", ", a.Ports));
            sb.Append(" (local ports ").Append(a.LocalPorts).Append(", remote ports ").Append(a.RemotePorts).Append(')');
            if (!String.IsNullOrEmpty(a.Processes))
                sb.Append(", process ").Append(a.Processes);
            sb.Append(". Allow the address, the port or the country if it's expected.");
            return sb.ToString();
        }

        static String FormatAge(TimeSpan t)
        {
            if (t.TotalMinutes < 2)
                return String.Concat(((int)t.TotalSeconds).ToString(CultureInfo.InvariantCulture), " seconds");
            if (t.TotalHours < 2)
                return String.Concat(((int)t.TotalMinutes).ToString(CultureInfo.InvariantCulture), " minutes");
            if (t.TotalDays < 2)
                return String.Concat(((int)t.TotalHours).ToString(CultureInfo.InvariantCulture), " hours");
            return String.Concat(((int)t.TotalDays).ToString(CultureInfo.InvariantCulture), " days");
        }

        /// <summary>
        /// Report an intruder: a problem (the exception tracker of the address), a warning in the log and an audit entry (a system event, no request)
        /// </summary>
        void Report(Intruder i, SmIntruderAlert alert)
        {
            var text = AlertText(alert);
            var ex = new IntruderAlertException(text);
            i.Tracker.OnException(ex);
            Manager.AddMessage(LogPrefix + text, MessageLevels.Warning);
            var audit = Audit;
            if (audit == null)
                return;
            try
            {
                var id = ApiAudit.GetId();
                audit.OnApiBegin(id, null, IntruderAudit, alert);
                audit.OnApiException(id, null, IntruderAudit, ex);
            }
            catch (Exception e)
            {
                NetEx.OnException(e);
            }
        }

        /// <summary>
        /// Remove the intruders that are now allowed (their problems disappear)
        /// </summary>
        void RemoveAllowedIntruders()
        {
            foreach (var x in Intruders)
            {
                var a = x.Value.Alert;
                if (AllowedIps.Contains(x.Key)
                    || ((a != null) && (a.Country.Length > 0) && AllowedCountries.Contains(a.Country))
                    || ((a != null) && a.Ports.All(p => AllowedPorts.Contains(p.ToString(CultureInfo.InvariantCulture)))))
                    Intruders.TryRemove(x);
            }
        }

        /// <summary>
        /// The actions of an intruder alert on the problems page: "Allow IP", "Allow port" (up to two ports) and "Allow country"
        /// </summary>
        /// <param name="system">The system of the problem</param>
        /// <param name="source">The source of the problem</param>
        /// <returns>The actions, null if the problem isn't an intruder alert</returns>
        public IEnumerable<ProblemAction> GetProblemActions(String system, String source)
        {
            if (!String.Equals(system, nameof(ServerManagerService), StringComparison.Ordinal) || (source == null) || !source.StartsWith(IntruderSourcePrefix, StringComparison.Ordinal))
                return null;
            if (!Intruders.TryGetValue(source.Substring(IntruderSourcePrefix.Length), out var i))
                return null;
            var a = i.Alert;
            var ip = i.Ip;
            var list = new List<ProblemAction>
            {
                new ProblemAction("Allow IP", String.Concat("Allow ", ip, ": connections with it are never flagged"), String.Concat("../ServerManager/AllowIp?\"", ip, "\""), null,
                    String.Concat("Allow ", ip, "? Connections with it will never be flagged as an intruder")),
            };
            if (a != null)
            {
                foreach (var port in a.Ports.Take(2))
                {
                    var ps = port.ToString(CultureInfo.InvariantCulture);
                    list.Add(new ProblemAction("Allow port " + ps, String.Concat("Allow port ", ps, ": connections on it are never flagged"), "../ServerManager/AllowPort?" + ps, null,
                        String.Concat("Allow port ", ps, "? Connections on it will never be flagged as an intruder (from any address)")));
                }
                if (a.Country.Length > 0)
                {
                    var name = SmIpInfo.CountryNameOf(a.Country);
                    var what = name.Length > 0 ? String.Concat(name, " (", a.Country, ")") : a.Country;
                    list.Add(new ProblemAction("Allow country " + a.Country, String.Concat("Allow ", what, ": its addresses are never flagged"), String.Concat("../ServerManager/AllowCountry?\"", a.Country, "\""), null,
                        String.Concat("Allow ", what, "? Its addresses will never be flagged as an intruder")));
                }
            }
            return list;
        }

        #endregion//Intruders

        #region Allow lists

        const String AllowedIpsKey = "ServerManager.Network.AllowedIps";
        const String AllowedPortsKey = "ServerManager.Network.AllowedPorts";
        const String AllowedCountriesKey = "ServerManager.Network.AllowedCountries";

        /// <summary>
        /// The "By" of the values of the service parameters
        /// </summary>
        const String ByParams = "Service parameters";

        SmAllowList AllowedIps;
        SmAllowList AllowedPorts;
        SmAllowList AllowedCountries;

        /// <summary>
        /// Allowed values: the ones of the service parameters (can't be removed) and the ones allowed by an administrator (stored in the application key value store)
        /// </summary>
        sealed class SmAllowList
        {
            public SmAllowList(String key, IEnumerable<String> config, Func<String, String> normalize)
            {
                Key = key;
                Normalize = normalize;
                var c = new Dictionary<String, SmAllowedEntry>(StringComparer.Ordinal);
                var invalid = new List<String>();
                var now = DateTime.UtcNow;
                foreach (var x in config ?? [])
                {
                    var v = normalize(x);
                    if (v == null)
                        invalid.Add(x);
                    else
                        c[v] = new SmAllowedEntry { Value = v, Allowed = now, By = ByParams };
                }
                Config = c;
                InvalidConfig = invalid;
                try
                {
                    foreach (var e in KeyValueStore.AllApp.TryGet<SmAllowedEntry[]>(key) ?? [])
                    {
                        var v = normalize(e?.Value);
                        if (v != null)
                            Stored[v] = e;
                    }
                }
                catch
                {
                }
                Rebuild();
            }

            readonly String Key;
            public readonly Func<String, String> Normalize;
            readonly Dictionary<String, SmAllowedEntry> Config;
            readonly Dictionary<String, SmAllowedEntry> Stored = new(StringComparer.Ordinal);
            public readonly List<String> InvalidConfig;

            /// <summary>
            /// All values (exact values, and the networks)
            /// </summary>
            volatile HashSet<String> Values = new(StringComparer.Ordinal);
            volatile System.Net.IPNetwork[] Networks = [];

            void Rebuild()
            {
                var v = new HashSet<String>(Config.Keys, StringComparer.Ordinal);
                lock (Stored)
                    v.UnionWith(Stored.Keys);
                var n = new List<System.Net.IPNetwork>();
                foreach (var x in v)
                    if ((x.IndexOf('/') >= 0) && System.Net.IPNetwork.TryParse(x, out var net))
                        n.Add(net);
                Networks = n.ToArray();
                Values = v;
            }

            /// <summary>
            /// True if a (normalized) value is allowed (an address is also allowed if it's in an allowed network)
            /// </summary>
            public bool Contains(String value)
            {
                if (Values.Contains(value))
                    return true;
                var n = Networks;
                if ((n.Length == 0) || !IPAddress.TryParse(value, out var a))
                    return false;
                foreach (var x in n)
                    if (x.Contains(a))
                        return true;
                return false;
            }

            public bool IsConfig(String value) => Config.ContainsKey(value);

            public bool Add(String value, String by)
            {
                lock (Stored)
                {
                    if (Config.ContainsKey(value) || Stored.ContainsKey(value))
                        return false;
                    Stored[value] = new SmAllowedEntry { Value = value, Allowed = DateTime.UtcNow, By = by ?? "" };
                    Save();
                }
                Rebuild();
                return true;
            }

            public bool Remove(String value)
            {
                lock (Stored)
                {
                    if (!Stored.Remove(value))
                        return false;
                    Save();
                }
                Rebuild();
                return true;
            }

            void Save() => KeyValueStore.AllApp.Set(Key, Stored.Values.OrderBy(x => x.Allowed).ToArray());

            /// <summary>
            /// All entries (the ones of the service parameters first)
            /// </summary>
            public List<SmAllowedEntry> GetEntries()
            {
                var l = Config.Values.ToList();
                lock (Stored)
                    l.AddRange(Stored.Values.Where(x => !Config.ContainsKey(x.Value)));
                return l;
            }
        }

        /// <summary>
        /// Normalize an address ("::ffff:1.2.3.4" = "1.2.3.4") or a network ("1.2.3.4/24" = "1.2.3.0/24")
        /// </summary>
        /// <returns>The normalized text, null if invalid</returns>
        static String NormalizeIp(String ip)
        {
            ip = ip?.Trim();
            if (String.IsNullOrEmpty(ip))
                return null;
            var slash = ip.IndexOf('/');
            //  IPAddress.TryParse accepts short IPv4 forms ("1.2.3" = 1.2.0.3, "16909060" = 1.2.3.4), never what is meant here
            if ((ip.IndexOf(':') < 0) && (ip.AsSpan(0, slash >= 0 ? slash : ip.Length).Count('.') != 3))
                return null;
            if (slash >= 0)
            {
                var i = slash;
                if (!IPAddress.TryParse(ip.AsSpan(0, i), out var na) || !Int32.TryParse(ip.AsSpan(i + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var bits))
                    return null;
                na = NetConnections.Normalize(na);
                var max = na.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128;
                if ((bits < 0) || (bits > max))
                    return null;
                //  Clear the host bits
                var b = na.GetAddressBytes();
                for (int x = bits; x < max; ++x)
                    b[x >> 3] &= (Byte)~(0x80 >> (x & 7));
                return String.Concat(new IPAddress(b).ToString(), "/", bits.ToString(CultureInfo.InvariantCulture));
            }
            if (!IPAddress.TryParse(ip, out var a))
                return null;
            return NetConnections.Normalize(a).ToString();
        }

        static String NormalizePort(String port)
            => Int32.TryParse(port?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var p) && (p > 0) && (p <= 65535) ? p.ToString(CultureInfo.InvariantCulture) : null;

        static String NormalizeCountry(String country)
        {
            var c = country?.Trim().ToUpperInvariant();
            return (c != null) && (c.Length == 2) && Char.IsAsciiLetterUpper(c[0]) && Char.IsAsciiLetterUpper(c[1]) ? c : null;
        }

        static String UserOf(HttpServerRequest context) => context?.Session?.Auth?.Username ?? "";

        bool Allow(SmAllowList list, String value, String what, HttpServerRequest context)
        {
            var v = list.Normalize(value);
            if (v == null)
                throw new HttpResponseException(400, String.Concat("Invalid ", what, ": \"", value ?? "", "\""));
            if (!list.Add(v, UserOf(context)))
                throw HttpResponseException.AlreadyDone(String.Concat(what.Substring(0, 1).ToUpperInvariant(), what.Substring(1), " ", v, " is already allowed"));
            RemoveAllowedIntruders();
            Manager.AddMessage(String.Concat(LogPrefix, UserOf(context), " allowed the ", what, " ", v, " (intruder alerts)"), MessageLevels.Info);
            context?.Server?.InvalidateCacheEverywhere();
            return true;
        }

        bool Deny(SmAllowList list, String value, String what, HttpServerRequest context)
        {
            var v = list.Normalize(value);
            if (v == null)
                throw new HttpResponseException(400, String.Concat("Invalid ", what, ": \"", value ?? "", "\""));
            if (list.IsConfig(v))
                throw HttpResponseException.Refused(String.Concat("The ", what, " ", v, " is allowed by the service parameters, change the parameters to remove it"));
            if (!list.Remove(v))
                throw HttpResponseException.AlreadyDone(String.Concat("The ", what, " ", v, " isn't allowed"));
            Manager.AddMessage(String.Concat(LogPrefix, UserOf(context), " removed the allowed ", what, " ", v, " (intruder alerts)"), MessageLevels.Info);
            context?.Server?.InvalidateCacheEverywhere();
            return true;
        }

        /// <summary>
        /// Allow an address or a network: connections with it are never flagged as intruders (stored in the application key value store)
        /// </summary>
        /// <param name="ip">The address (ex: "203.0.113.7") or network (ex: "203.0.113.0/24")</param>
        /// <param name="context">The request</param>
        /// <returns>True if it was allowed</returns>
        /// <exception cref="HttpResponseException">400: invalid address, 409: already allowed</exception>
        [WebApi]
        [WebApiAuth(Roles.Admin)]
        [WebApiAudit(AuditGroup)]
        public bool AllowIp(String ip, HttpServerRequest context) => Allow(AllowedIps, ip, "address", context);

        /// <summary>
        /// Remove an allowed address or network (connections with it may be flagged as intruders again)
        /// </summary>
        /// <param name="ip">The address or network</param>
        /// <param name="context">The request</param>
        /// <returns>True if it was removed</returns>
        /// <exception cref="HttpResponseException">400: invalid address, 409: not allowed, or allowed by the service parameters (can't be removed here)</exception>
        [WebApi]
        [WebApiAuth(Roles.Admin)]
        [WebApiAudit(AuditGroup)]
        public bool DenyIp(String ip, HttpServerRequest context) => Deny(AllowedIps, ip, "address", context);

        /// <summary>
        /// Allow a port: connections on it are never flagged as intruders (the local port of an incoming connection, else the remote port, stored in the application key value store)
        /// </summary>
        /// <param name="port">The port</param>
        /// <param name="context">The request</param>
        /// <returns>True if it was allowed</returns>
        /// <exception cref="HttpResponseException">400: invalid port, 409: already allowed</exception>
        [WebApi]
        [WebApiAuth(Roles.Admin)]
        [WebApiAudit(AuditGroup)]
        public bool AllowPort(int port, HttpServerRequest context) => Allow(AllowedPorts, port.ToString(CultureInfo.InvariantCulture), "port", context);

        /// <summary>
        /// Remove an allowed port (connections on it may be flagged as intruders again)
        /// </summary>
        /// <param name="port">The port</param>
        /// <param name="context">The request</param>
        /// <returns>True if it was removed</returns>
        /// <exception cref="HttpResponseException">400: invalid port, 409: not allowed, or allowed by the service parameters (can't be removed here)</exception>
        [WebApi]
        [WebApiAuth(Roles.Admin)]
        [WebApiAudit(AuditGroup)]
        public bool DenyPort(int port, HttpServerRequest context) => Deny(AllowedPorts, port.ToString(CultureInfo.InvariantCulture), "port", context);

        /// <summary>
        /// Allow a country: its addresses are never flagged as intruders (stored in the application key value store)
        /// </summary>
        /// <param name="isoCountry">The two letter ISO 3166 country code, ex: "SE"</param>
        /// <param name="context">The request</param>
        /// <returns>True if it was allowed</returns>
        /// <exception cref="HttpResponseException">400: invalid country code, 409: already allowed</exception>
        [WebApi]
        [WebApiAuth(Roles.Admin)]
        [WebApiAudit(AuditGroup)]
        public bool AllowCountry(String isoCountry, HttpServerRequest context) => Allow(AllowedCountries, isoCountry, "country", context);

        /// <summary>
        /// Remove an allowed country (its addresses may be flagged as intruders again)
        /// </summary>
        /// <param name="isoCountry">The two letter ISO 3166 country code</param>
        /// <param name="context">The request</param>
        /// <returns>True if it was removed</returns>
        /// <exception cref="HttpResponseException">400: invalid country code, 409: not allowed, or allowed by the service parameters (can't be removed here)</exception>
        [WebApi]
        [WebApiAuth(Roles.Admin)]
        [WebApiAudit(AuditGroup)]
        public bool DenyCountry(String isoCountry, HttpServerRequest context) => Deny(AllowedCountries, isoCountry, "country", context);

        static String RemoveValue(SmAllowList list, SmAllowedEntry e) => String.Concat("-,", e.Value, ",", list.IsConfig(e.Value) ? "" : "1");

        /// <summary>
        /// The addresses and networks that are never flagged as intruders: when and by whom they were allowed
        /// </summary>
        /// <param name="r">Table request: paging, sorting and filtering</param>
        /// <returns>Table data, one row per address or network</returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        [WebApiClientCache(4)]
        [WebApiRequestCache(3)]
        public TableData AllowedIpsTable(TableDataRequest r)
        {
            var l = AllowedIps;
            return TableDataTools.Get(r, 10000, l.GetEntries().Select(e => new SmAllowedIp { Ip = e.Value, Allowed = e.Allowed, By = e.By, Actions = RemoveValue(l, e) }));
        }

        /// <summary>
        /// The ports that are never flagged as intruders (the local port of an incoming connection, else the remote port): when and by whom they were allowed
        /// </summary>
        /// <param name="r">Table request: paging, sorting and filtering</param>
        /// <returns>Table data, one row per port</returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        [WebApiClientCache(4)]
        [WebApiRequestCache(3)]
        public TableData AllowedPortsTable(TableDataRequest r)
        {
            var l = AllowedPorts;
            return TableDataTools.Get(r, 10000, l.GetEntries().Select(e =>
            {
                var port = Int32.Parse(e.Value, CultureInfo.InvariantCulture);
                return new SmAllowedPort { Port = port, Service = WellKnownPorts.GetValueOrDefault(port, ""), Allowed = e.Allowed, By = e.By, Actions = RemoveValue(l, e) };
            }));
        }

        /// <summary>
        /// The countries whose addresses are never flagged as intruders: when and by whom they were allowed
        /// </summary>
        /// <param name="r">Table request: paging, sorting and filtering</param>
        /// <returns>Table data, one row per country</returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        [WebApiClientCache(4)]
        [WebApiRequestCache(3)]
        public TableData AllowedCountriesTable(TableDataRequest r)
        {
            var l = AllowedCountries;
            return TableDataTools.Get(r, 10000, l.GetEntries().Select(e => new SmAllowedCountry { Country = e.Value, Name = SmIpInfo.CountryNameOf(e.Value), Allowed = e.Allowed, By = e.By, Actions = RemoveValue(l, e) }));
        }

        /// <summary>
        /// Names of some well known ports (shown in the allowed ports table)
        /// </summary>
        static readonly IReadOnlyDictionary<int, String> WellKnownPorts = new Dictionary<int, String>
        {
            { 20, "FTP data" }, { 21, "FTP" }, { 22, "SSH" }, { 23, "Telnet" }, { 25, "SMTP" }, { 53, "DNS" }, { 80, "HTTP" }, { 110, "POP3" },
            { 123, "NTP" }, { 143, "IMAP" }, { 389, "LDAP" }, { 443, "HTTPS" }, { 445, "SMB" }, { 465, "SMTPS" }, { 587, "SMTP submission" },
            { 636, "LDAPS" }, { 993, "IMAPS" }, { 995, "POP3S" }, { 1433, "SQL Server" }, { 1723, "PPTP" }, { 3306, "MySQL" }, { 3389, "Remote desktop" },
            { 5222, "XMPP" }, { 5432, "PostgreSQL" }, { 5900, "VNC" }, { 6379, "Redis" }, { 8080, "HTTP (alternative)" }, { 8443, "HTTPS (alternative)" },
            { 27017, "MongoDB" },
        };

        #endregion//Allow lists
    }
}
