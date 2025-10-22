
using System.Threading.Tasks;
using System;
using System.Threading;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Linq;

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

    }
}
