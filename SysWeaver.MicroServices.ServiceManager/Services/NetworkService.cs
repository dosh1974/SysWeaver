using System;
using System.Threading.Tasks;


namespace SysWeaver.MicroService
{

    /// <summary>
    /// Service that wait for the computer to get a LAN ip.
    /// </summary>
    [IsMicroService]
    public sealed class NetworkService
    {
        const String Prefix = "[Network] ";

        public NetworkService(ServiceManager manager, NetworkServiceParams p = null)
        {
            p = p ?? new NetworkServiceParams();
            var timeOut = Math.Max(5, p.TimeOutSeconds);
            var ms = p.MustStartWith?.Trim();
            var ip = NetworkTools.GetAnyLanIP(ms);
            if (ip == null)
            {
                manager.AddMessage(Prefix + "Waiting up to " + timeOut + " seconds for a valid LAN ip");
                ip = NetworkTools.WaitForLanIp(timeOut, ms);
            }
            if (ip == null)
            {
                var t = p.FailIfNoIpFound;
                manager.AddMessage(Prefix + "No LAN ip found!", t ? MessageLevels.Error : MessageLevels.Warning);
                if (t)
                    throw new Exception(Prefix + "No LAN ip found!");
            }else
            {
                var ips = NetworkTools.GetAllLanIps();
                manager.AddMessage(Prefix + "LAN ip's:");
                using (manager.Tab())
                {
                    foreach (var x in ips)
                        manager.AddMessage(Prefix + x);
                }
            }
            if (p.WaitForInternet)
            {
                var s = p.InternetTimeOutSeconds;
                if (s > 0)
                {
                    var iip = WaitInet(manager, s).RunAsync();
                    if (iip == null)
                    {
                        var t = p.FailIfNoInternetFound;
                        manager.AddMessage(Prefix + "No internet connection!", t ? MessageLevels.Error : MessageLevels.Warning);
                        if (t)
                            throw new Exception(Prefix + "No internet connection!");
                    }else
                    {
                        manager.AddMessage(Prefix + "Internet found at: " + iip);
                    }
                }
            }
        }

        static async Task<String> WaitInet(ServiceManager manager, int timeOut)
        {
            var ip = await NetworkTools.IsConnectedToInternetAsync().ConfigureAwait(false);
            if (ip != null)
                return ip;
            manager.AddMessage(Prefix + "Waiting up to " + timeOut + " seconds for an internet connection");
            return await NetworkTools.WaitForInternetConnectionAsync(timeOut).ConfigureAwait(false);
        }



    }


}
