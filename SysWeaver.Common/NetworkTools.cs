
using System.Threading.Tasks;
using System;
using System.Threading;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Linq;
using System.Net.Sockets;

namespace SysWeaver
{
    /// <summary>
    /// Network helpers: local / LAN ip addresses, routable addresses and internet connectivity checks
    /// </summary>
    public static class NetworkTools
    {
        static readonly IReadOnlySet<String> ValidIpStarts = ReadOnlyData.Set(StringComparer.Ordinal,
                "192", "172", "10"
            );

        /// <summary>
        /// Get the first valid LAN ip found (an IPv4 address of the local machine where the first number is 192, 172 or 10)
        /// </summary>
        /// <param name="mustStartWith">If not null or empty, the first number in the IP must match this, valid values are: "192", "172", "10" (any other value will always return null)</param>
        /// <returns>The first LAN ip (in the order of <see cref="GetLocalIps"/>) or null if no LAN ip is found</returns>
        /// <exception cref="NetworkInformationException">If the network interfaces can't be enumerated</exception>
        public static IPAddress GetAnyLanIP(String mustStartWith = null)
        {
            var validIpStarts = ValidIpStarts;
            foreach (var x in GetLocalIps())
            {
                try
                {
                    var part = x.ToString().Split('.')[0];
                    if (validIpStarts.Contains(part))
                        if (String.IsNullOrEmpty(mustStartWith) || (mustStartWith == part))
                            return x;
                }
                catch
                {
                }
            }
            return null;
        }

        /// <summary>
        /// Get all LAN ip's (the IPv4 addresses of the local machine where the first number is 192, 172 or 10)
        /// </summary>
        /// <returns>All LAN ip's (in the order of <see cref="GetLocalIps"/>), an empty list if no LAN ip is found (never null)</returns>
        /// <exception cref="NetworkInformationException">If the network interfaces can't be enumerated</exception>
        public static List<IPAddress> GetAllLanIps()
        {
            var validIpStarts = ValidIpStarts;
            List<IPAddress> ips = new List<IPAddress>();
            foreach (var x in GetLocalIps())
            {
                try
                {
                    if (validIpStarts.Contains(x.ToString().Split('.')[0]))
                        ips.Add(x);
                }
                catch
                {
                }
            }
            return ips;
        }


        /// <summary>
        /// Wait for a LAN ip to be available (useful when running as a service and the network stack starts after the current service)
        /// </summary>
        /// <param name="maxSeconds">Maximum number of seconds to wait (zero or negative will only check once)</param>
        /// <param name="mustStartWith">If not null or empty, the first number in the IP must match this, valid values are: "192", "172", "10"</param>
        /// <returns>The first found LAN ip or null if none found within the time frame</returns>
        /// <exception cref="NetworkInformationException">If the network interfaces can't be enumerated</exception>
        /// <remarks>Checks for a LAN ip once every second (blocking the calling thread), see <see cref="GetAnyLanIP(string)"/></remarks>
        public static IPAddress WaitForLanIp(int maxSeconds = 30, String mustStartWith = null)
        {
            var start = DateTime.UtcNow;
            for (; ; )
            {
                var t = GetAnyLanIP(mustStartWith);
                if (t != null)
                    return t;
                if ((DateTime.UtcNow - start).TotalSeconds > maxSeconds)
                    return null;
                Thread.Sleep(1000);
            }
        }

        /// <summary>
        /// Wait for a LAN ip to be available (useful when running as a service and the network stack starts after the current service)
        /// </summary>
        /// <param name="maxSeconds">Maximum number of seconds to wait (zero or negative will only check once)</param>
        /// <param name="mustStartWith">If not null or empty, the first number in the IP must match this, valid values are: "192", "172", "10"</param>
        /// <returns>The first found LAN ip or null if none found within the time frame</returns>
        /// <exception cref="NetworkInformationException">If the network interfaces can't be enumerated</exception>
        /// <remarks>Checks for a LAN ip once every second, see <see cref="GetAnyLanIP(string)"/></remarks>
        public static async Task<IPAddress> WaitForLanIpAsync(int maxSeconds = 30, String mustStartWith = null)
        {
            var start = DateTime.UtcNow;
            for (; ; )
            {
                var t = GetAnyLanIP(mustStartWith);
                if (t != null)
                    return t;
                if ((DateTime.UtcNow - start).TotalSeconds > maxSeconds)
                    return null;
                await Task.Delay(1000).ConfigureAwait(false);
            }
        }


        /// <summary>
        /// Get the unicast ip addresses (IPv4 and IPv6) of all ethernet and wireless network interfaces of the local machine
        /// </summary>
        /// <returns>The distinct ip addresses sorted by their string representation, IPv4 link local (169.254.x.x) and unspecified (0.0.0.0) addresses are excluded</returns>
        /// <exception cref="NetworkInformationException">If the network interfaces can't be enumerated</exception>
        public static IEnumerable<IPAddress> GetLocalIps()
        {
            HashSet<IPAddress> addresses = new HashSet<IPAddress>();
            foreach (NetworkInterface netInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                bool isOk = false;
                switch (netInterface.NetworkInterfaceType)
                {
                    case NetworkInterfaceType.Ethernet:
                    case NetworkInterfaceType.Ethernet3Megabit:
                    case NetworkInterfaceType.FastEthernetFx:
                    case NetworkInterfaceType.FastEthernetT:
                    case NetworkInterfaceType.GigabitEthernet:
                    case NetworkInterfaceType.Wireless80211:
                        isOk = true;
                        break;
                }
                if (!isOk)
                    continue;
                IPInterfaceProperties ipProps = netInterface.GetIPProperties();
                foreach (UnicastIPAddressInformation addr in ipProps.UnicastAddresses)
                {
                    var add = addr.Address;
                    var adds = add.ToString();
                    if (adds.StartsWith("169.254.", StringComparison.Ordinal))
                        continue;
                    if (adds == "0.0.0.0")
                        continue;
                    addresses.Add(addr.Address);
                }
            }
            return addresses.OrderBy(x => x.ToString());
        }

        /// <summary>
        /// Check if an ip address is routable (i.e on the internet)
        /// </summary>
        /// <param name="addr">The ip address to check (IPv4 or IPv6)</param>
        /// <returns>False if the address is null, loopback, unspecified, private (10/8, 172.16/12, 192.168/16), link local (169.254/16, fe80::/10), "this" network (0/8),
        /// site local (fec0::/10) or unique local (fc00::/7), else true</returns>
        /// <remarks>IPv4 mapped IPv6 addresses (::ffff:a.b.c.d) are checked as the IPv4 address</remarks>
        public static bool IsRoutableAddress(IPAddress addr)
        {
            if (addr == null)
            {
                return false;
            }
            if (addr.IsIPv4MappedToIPv6)
                addr = addr.MapToIPv4();
            if (IPAddress.IsLoopback(addr) || addr.Equals(IPAddress.Any) || addr.Equals(IPAddress.IPv6Any))
            {   // Loopback (127/8, ::1) or unspecified (0.0.0.0, ::)
                return false;
            }
            else if (addr.AddressFamily == AddressFamily.InterNetworkV6)
            {
                return !addr.IsIPv6LinkLocal && !addr.IsIPv6SiteLocal && !addr.IsIPv6UniqueLocal;
            }
            else // IPv4
            {
                Span<Byte> bytes = stackalloc Byte[16];
                if (!addr.TryWriteBytes(bytes, out var len))
                    throw new InvalidOperationException("Failed to get the bytes of the ip address " + addr);
                var b0 = bytes[0];
                if (b0 == 10)
                {   // Class A network
                    return false;
                }
                else if (b0 == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                {   // Class B network
                    return false;
                }
                else if (b0 == 192 && bytes[1] == 168)
                {   // Class C network
                    return false;
                }
                else if ((b0 == 169 && bytes[1] == 254) || b0 == 0)
                {   // Link local or "this" network
                    return false;
                }
                else
                {   // None of the above, so must be routable
                    return true;
                }
            }
        }



        static readonly String[] InternetChecks = [
            "8.8.8.8", // Google DNS
            "1.1.1.1", // Cloud flare DNS
            "www.microsoft.com",
            "www.cnn.com",
            "www.alibaba.com",
            "www.aparat.com", // Iran
            "www.baidu.com", // China
            // TODO: Add more if not reachable from some country due to firewalls
        ];

        /// <summary>
        /// Check if internet is available
        /// </summary>
        /// <param name="maxHops">Maximum number of hops when pinging, 1 to 255 (if the application is running where deep into some internal network, VM's in VM's etc, then maybe increase this)</param>
        /// <returns>The IP of the closest route to the internet (the first routable address found when pinging some well known internet hosts), or null if no internet connection is found</returns>
        /// <exception cref="ArgumentOutOfRangeException">If <paramref name="maxHops"/> is less than 1 or greater than 255 (thrown synchronously)</exception>
        /// <remarks>Some well known hosts (DNS servers and web sites) are pinged with an increasing time to live (ttl) until a reply comes from a routable address (see <see cref="IsRoutableAddress(IPAddress)"/>)</remarks>
        public static Task<String> IsConnectedToInternetAsync(int maxHops = 30)
        {
            ValidateMaxHops(maxHops);
            return InternalIsConnectedToInternetAsync(maxHops);
        }

        static void ValidateMaxHops(int maxHops)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(maxHops, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(maxHops, 255);
        }

        static async Task<String> InternalIsConnectedToInternetAsync(int maxHops)
        {
            HashSet<Task<String>> tasks = new (InternetChecks.Select(x => InternalCheckIp(x, maxHops)));
            while (tasks.Count > 0)
            {
                var t = await Task.WhenAny(tasks).ConfigureAwait(false);
                var ip = t.GetAwaiter().GetResult();
                //  If any return an IP, don't wait for the rest
                if (ip != null)
                    return ip;
                tasks.Remove(t);
            }
            return null;
        }


        /// <summary>
        /// Wait for an internet connection
        /// </summary>
        /// <param name="maxSeconds">Maximum number of seconds to wait (zero or negative will only check once)</param>
        /// <param name="maxHops">Maximum number of hops when pinging, 1 to 255 (if the application is running where deep into some internal network, VM's in VM's etc, then maybe increase this)</param>
        /// <returns>The IP of the closest route to the internet, or null if no internet connection is found within the time frame</returns>
        /// <exception cref="ArgumentOutOfRangeException">If <paramref name="maxHops"/> is less than 1 or greater than 255 (thrown synchronously)</exception>
        /// <remarks>Calls <see cref="IsConnectedToInternetAsync(int)"/> until an internet connection is found, waiting one second between the checks</remarks>
        public static Task<String> WaitForInternetConnectionAsync(int maxSeconds = 30, int maxHops = 30)
        {
            ValidateMaxHops(maxHops);
            return InternalWaitForInternetConnectionAsync(maxSeconds, maxHops);
        }

        static async Task<String> InternalWaitForInternetConnectionAsync(int maxSeconds, int maxHops)
        {
            var start = DateTime.UtcNow;
            for (; ; )
            {
                var ip = await InternalIsConnectedToInternetAsync(maxHops).ConfigureAwait(false);
                if (ip != null)
                    return ip;
                if ((DateTime.UtcNow - start).TotalSeconds > maxSeconds)
                    return null;
                await Task.Delay(1000).ConfigureAwait(false);
            }
        }


        static async Task<String> InternalCheckIp(String ip = "8.8.8.8", int maxHops = 30)
        {
            if (!Char.IsNumber(ip[0]))
            {
                try
                {
                    ip = (await Dns.GetHostEntryAsync(ip).ConfigureAwait(false)).AddressList.FirstOrDefault()?.ToString();
                    if (ip == null)
                        return null;
                }
                catch
                {
                    return null;
                }
            }

            // Keep pinging further along the line from here to google 
            // until we find a response that is from a routable address
            for (int ttl = 1; ttl <= maxHops; ttl++)
            {
                var options = new PingOptions(ttl, true);
                byte[] buffer = GC.AllocateUninitializedArray<Byte>(32);
                PingReply reply;
                try
                {
                    using (var pinger = new Ping())
                    {
                        reply = await pinger.SendPingAsync(ip, 10000, buffer, options).ConfigureAwait(false);
                    }
                }
                catch// (Exception pingex)
                {
                    //Debug.Print($"Ping exception (probably due to no network connection or recent change in network conditions), hence not connected to internet. Message: {pingex.Message}");
                    return null;
                }
                if (reply.Status != IPStatus.TtlExpired && reply.Status != IPStatus.Success)
                {
                    return null;
                }
                if (IsRoutableAddress(reply.Address))
                {
                    //Debug.Print("That's routable, so we must be connected to the internet.");
                    return reply.Address.ToString();
                }
            }
            return null;
        }




    }
}
