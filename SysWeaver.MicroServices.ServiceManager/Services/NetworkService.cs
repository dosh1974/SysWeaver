using System;


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
            var t = p.FailIfNoIpFound;
            if (ip == null)
            {
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
            /*if (p.WaitForInternet)
            {

            }*/
        }
    }


}
