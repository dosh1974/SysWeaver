using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SysWeaver.IpLocation.Caches;
using SysWeaver.IpLocation.Sources;
using SysWeaver.MicroService;

namespace SysWeaver.IpLocation
{

    [RequiredDep<IIpLocationSource>]
    [OptionalDep<IIpLocationCache>]
    [WebApiUrl("IpLocation")]
    public sealed class IpLocationService : IHaveStats, IPerfMonitored
    {
        public IpLocationService(ServiceManager manager, IpLocationParams p = null)
        {
            p = p ?? new IpLocationParams();
            Cache = manager.TryGet<IIpLocationCache>() ?? new IpLocationMemoryCache(new IpLocationMemoryCacheParams { MaxCachedMinutes = p.MaxCachedMinutes });
            Sources = manager.GetAll<IIpLocationSource>(ServiceInstanceTypes.Any, ServiceInstanceOrders.Oldest).ToArray();
            InternalGetLocationFunc = InternalGetLocation;
            MaxRetryCount = Math.Max(1, p.MaxRetryCount);
            MinWaitMs = Math.Max(10, p.MinWaitMs);
            MaxWaitMs = Math.Max(MinWaitMs * 5, p.MaxWaitMs);
            Step = (MaxWaitMs * 2 - MinWaitMs) / MaxRetryCount;
        }

        readonly int Step;
        readonly int MaxRetryCount;
        readonly int MinWaitMs;
        readonly int MaxWaitMs;


        readonly IIpLocationCache Cache;
        readonly IIpLocationSource[] Sources;

        /// <summary>
        /// Get the locatrion from an ip address
        /// </summary>
        /// <param name="ip">Ip address</param>
        /// <returns>Location or null</returns>
        [WebApi]
        [WebApiAuth(Roles.Service)]
        public async Task<IpLocation> GetLocation(string ip)
        {
            using var _ = PerfMon.Track(nameof(GetLocation));
            var t = await Cache.Get(ip, InternalGetLocationFunc).ConfigureAwait(false);
            return t == null ? null : new IpLocation(t);

        }

        readonly Func<String, Task<IpLocation>> InternalGetLocationFunc;
        readonly ExceptionTracker SourceFails = new ExceptionTracker();

        public PerfMonitor PerfMon { get; } = new PerfMonitor("IpLocation");

        async Task<IpLocation> InternalGetLocation(string ip)
        {
            var pf = PerfMon;
            using var _ = pf.Track(nameof(InternalGetLocation));
            var wait = MinWaitMs;
            var max = MaxWaitMs;
            var step = Step;
            var s = Sources;
            var sl = s.Length;
            for (int i = 0; i < 10; ++ i)
            {
                for (int si = 0; si < sl; ++ si)
                {
                    try
                    {
                        var ss = s[si];
                        using var __ = pf.Track("LookUp_" + ss.Name);
                        return await ss.LookUp(ip).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        SourceFails.OnException(ex);
                    }
                }
                await Task.Delay(wait).ConfigureAwait(false);
                wait += step;
                if (wait > max)
                    wait = max;
            }
            return null;
        }

        public IEnumerable<Stats> GetStats()
        {
            foreach (var x in SourceFails.GetStats("IpLocation", "Source."))
                yield return x;
        }
    }



}
