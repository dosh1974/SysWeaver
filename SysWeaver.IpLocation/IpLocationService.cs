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

        /// <summary>
        /// Try the sources in order, at most MaxRetryCount rounds with a growing wait between the rounds.
        /// A source that times out is not tried again for this ip (it already used up its whole time out, a retry would only add to it),
        /// when all sources timed out the look up ends at once.
        /// </summary>
        async Task<IpLocation> InternalGetLocation(string ip)
        {
            var pf = PerfMon;
            using var _ = pf.Track(nameof(InternalGetLocation));
            var wait = MinWaitMs;
            var max = MaxWaitMs;
            var step = Step;
            var s = Sources;
            var sl = s.Length;
            var rounds = MaxRetryCount;
            bool[] timedOut = null;
            var live = sl;
            for (int i = 0; i < rounds; ++ i)
            {
                for (int si = 0; si < sl; ++ si)
                {
                    if ((timedOut != null) && timedOut[si])
                        continue;
                    try
                    {
                        var ss = s[si];
                        using var __ = pf.Track("LookUp_" + ss.Name);
                        return await ss.LookUp(ip).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        SourceFails.OnException(ex);
                        if (IsTimeout(ex))
                        {
                            (timedOut ??= new bool[sl])[si] = true;
                            --live;
                        }
                    }
                }
                if ((live <= 0) || ((i + 1) >= rounds))
                    break;
                await Task.Delay(wait).ConfigureAwait(false);
                wait += step;
                if (wait > max)
                    wait = max;
            }
            return null;
        }

        /// <summary>
        /// True if the exception (or an inner exception) is a time out (a remote connection or HttpClient time out)
        /// </summary>
        static bool IsTimeout(Exception ex)
        {
            for (int depth = 0; (ex != null) && (depth < 8); ++depth, ex = ex.InnerException)
            {
                if ((ex is TimeoutException) || (ex is TaskCanceledException))
                    return true;
            }
            return false;
        }

        public IEnumerable<Stats> GetStats()
        {
            foreach (var x in SourceFails.GetStats("IpLocation", "Source."))
                yield return x;
        }
    }



}
