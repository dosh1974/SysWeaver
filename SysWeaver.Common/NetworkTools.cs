
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
    public static class NetworkTools
    {
        static readonly IReadOnlySet<String> ValidIpStarts = ReadOnlyData.Set(StringComparer.Ordinal,
                "192", "172", "10"
            );

        /// <summary>
        /// Get the first valid LAN ip found
        /// </summary>
        /// <param name="mustStartWith">If not null, the first number in the IP must match this, valid values are: "192", "172", "10"</param>
        /// <returns>null if no LAN ip is found</returns>
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
        /// Get all LAN ip's
        /// </summary>
        /// <returns>null if no LAN ip is found</returns>
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
        /// <param name="maxSeconds">Maximum number of seconds to wait</param>
        /// <param name="mustStartWith">If not null, the first number in the IP must match this, valid values are: "192", "172", "10"</param>
        /// <returns>The first found LAN ip or null if none found within the time frame</returns>
        public static IPAddress WaitForLanIp(int maxSeconds = 30, String mustStartWith = null)
        {
            var start = DateTime.UtcNow;
            var validIpStarts = ValidIpStarts;
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
        /// <param name="maxSeconds">Maximum number of seconds to wait</param>
        /// <param name="mustStartWith">If not null, the first number in the IP must match this, valid values are: "192", "172", "10"</param>
        /// <returns>The first found LAN ip or null if none found within the time frame</returns>
        public static async Task<IPAddress> WaitForLanIpAsync(int maxSeconds = 30, String mustStartWith = null)
        {
            var start = DateTime.UtcNow;
            var validIpStarts = ValidIpStarts;
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
        /// Return a list of LAN ip's
        /// </summary>
        /// <returns></returns>
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
                    if (adds.StartsWith("169.254."))
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
        /// <param name="addr"></param>
        /// <returns></returns>
        public static bool IsRoutableAddress(IPAddress addr)
        {
            if (addr == null)
            {
                return false;
            }
            else if (addr.AddressFamily == AddressFamily.InterNetworkV6)
            {
                return !addr.IsIPv6LinkLocal && !addr.IsIPv6SiteLocal;
            }
            else // IPv4
            {
                byte[] bytes = addr.GetAddressBytes();
                if (bytes[0] == 10)
                {   // Class A network
                    return false;
                }
                else if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                {   // Class B network
                    return false;
                }
                else if (bytes[0] == 192 && bytes[1] == 168)
                {   // Class C network
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
        /// <param name="maxHops">Maximum number of hops when pinging (if the application is running where deep into some internal network, VM's in VM's etc, then maybe imcrease this)</param>
        /// <returns>The IP of the closest route to the internet</returns>
        public static async Task<String> IsConnectedToInternetAsync(int maxHops = 30)
        {
            HashSet<Task<String>> tasks = new (InternetChecks.Select(x => InternalCheckIp(x, maxHops)));
            while (tasks.Count > 0)
            {
                var t = await Task.WhenAny(tasks).ConfigureAwait(false);
                var ip = t.Result;
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
        /// <param name="maxSeconds">Maximum number of seconds to wait</param>
        /// <param name="maxHops">Maximum number of hops when pinging (if the application is running where deep into some internal network, VM's in VM's etc, then maybe imcrease this)</param>
        /// <returns>The IP of the closest route to the internet</returns>
        public static async Task<String> WaitForInternetConnectionAsync(int maxSeconds = 30, int maxHops = 30)
        {
            var start = DateTime.UtcNow;
            for (; ; )
            {
                var ip = await IsConnectedToInternetAsync(maxHops).ConfigureAwait(false);
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
