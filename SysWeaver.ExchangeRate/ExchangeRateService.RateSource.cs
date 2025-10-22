using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Serialization;

namespace SysWeaver.ExchangeRate
{


    public sealed partial class ExchangeRateService
    {
        sealed class RateSource : IDisposable
        {
#if DEBUG
            public override string ToString() => S?.ToString();
#endif//DEBUG

            public void Dispose()
            {
                Interlocked.Exchange(ref UpdateTask, null)?.Dispose();
            }

            public readonly ExceptionTracker Fails = new ExceptionTracker();

            public readonly IRateSource S;

            public readonly TimeSpan FailTime;

            long NextUpdate;


            public long NextUpdateTick => Interlocked.Read(ref NextUpdate);

            public void StartUpdating(TimeSpan min, bool wasError = false)
            {
                var utcNow = DateTime.UtcNow;
                var utcNowTicks = utcNow.Ticks;
                var updateTicks = TimeSpan.TicksPerMinute * S.RefreshMinutes;
                if (wasError)
                    updateTicks = (updateTicks + 7) >> 3;
                var nextPeriod = (utcNowTicks / updateTicks) + 1;
                var nextUpdate = new DateTime(nextPeriod * updateTicks, DateTimeKind.Utc);
                if ((nextUpdate - utcNow) < min)
                {
                    ++nextPeriod;
                    nextUpdate = new DateTime(nextPeriod * updateTicks, DateTimeKind.Utc);
                }
                Interlocked.Exchange(ref NextUpdate, nextUpdate.Ticks);
                Interlocked.Exchange(ref UpdateTask, Scheduler.Add(nextUpdate, async () =>
                {
                    var ok = await UpdateRates().ConfigureAwait(false);
                    StartUpdating(min, !ok);
                }));
            }

            IDisposable UpdateTask;

            public RateSource(IRateSource s, String cacheFolder, PerfMonitor pf, IReadOnlyDictionary<String, String> oldToNewMap)
            {
                OldToNewMap = oldToNewMap;
                PerfMon = pf;
                S = s;
                Cache = new RateDiscCache(cacheFolder, s.Source, Fails);
                Fail = TimeSpan.FromMinutes(1.3 * s.RefreshMinutes);
                var r = Cache.TryReadRates();
                if (r != null)
                    CurrentRates = new RateCache(r, oldToNewMap);
            }

            readonly IReadOnlyDictionary<String, String> OldToNewMap;
            readonly RateDiscCache Cache;

            readonly PerfMonitor PerfMon;

            public readonly TimeSpan Fail;

            public async Task<bool> UpdateRates()
            {
                var s = S;
                using var _ = PerfMon.Track("Update " + s.Source);
                try
                {
                    var rates = await s.GetRates().ConfigureAwait(false);
                    if (rates != null)
                    {
                        Cache.TrySaveRates(rates);
                        var r = new RateCache(rates, OldToNewMap);
                        Interlocked.Exchange(ref CurrentRates, r);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Fails.OnException(ex);
                }
                return false;

            }

            public RateCache Rates => CurrentRates;

            volatile RateCache CurrentRates;
        }


    }

}
