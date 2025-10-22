using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SysWeaver.Data;
using SysWeaver.IsoData;
using SysWeaver.MicroService;

namespace SysWeaver.ExchangeRate
{

    [RequiredDep<IRateSource>]
    [WebApiUrl("ExchangeRates")]
    [WebMenuPath(null, "Debug/ExchangeRate", "Exchange rate", "Debug data for the Exchange Rate Service", "IconRefund", 10)]
    public sealed partial class ExchangeRateService : IDisposable, IPerfMonitored, IHaveStats
    {
        public ExchangeRateService(ServiceManager manager, ExchangeRateParams p = null)
        {
            p = p ?? new ExchangeRateParams();
            Manager = manager;

            Dictionary<String, String> newToOldMap = new(StringComparer.Ordinal);
            foreach (var x in p.OldToNewRateMap?.Nullable())
            {
                var kv = x?.Trim();
                if (String.IsNullOrEmpty(kv))
                    continue;
                var r = kv.Split('=');
                if (r.Length != 2)
                    throw new ArgumentException("Must contain a Key=Value pair, found " + x.ToQuoted(), nameof(p.OldToNewRateMap));
                var k = r[0].Trim().FastToUpper();
                var v = r[1].Trim().FastToUpper();
                if (k.Length <= 0)
                    throw new ArgumentException("Must contain a Key=Value pair, found " + x.ToQuoted(), nameof(p.OldToNewRateMap));
                if (v.Length <= 0)
                    throw new ArgumentException("Must contain a Key=Value pair, found " + x.ToQuoted(), nameof(p.OldToNewRateMap));
                newToOldMap.Add(k, v);
            }
            var cacheFolder = p.CacheFolder;
            List<RateSource> sources = new List<RateSource>();
            Sources = sources;
            List<Task> updateNow = new List<Task>();
            var utcNowTicks = DateTime.UtcNow.Ticks;
            var maxAge = TimeSpan.FromMinutes(3);
            var maxSchedule = TimeSpan.FromMinutes(1);
            var pf = PerfMon;
            foreach (var x in manager.GetAll<IRateSource>(ServiceInstanceTypes.LocalOnly, ServiceInstanceOrders.Oldest))
            {
                var s = new RateSource(x, cacheFolder, pf, newToOldMap);
                sources.Add(s);
                var updateTicks = TimeSpan.TicksPerMinute * x.RefreshMinutes;
                var nextPeriod = (utcNowTicks / updateTicks) + 1;
                var nextUpdate = new DateTime(nextPeriod * updateTicks, DateTimeKind.Utc);
                var cr = s.Rates;
                if (cr == null)
                {
                    updateNow.Add(s.UpdateRates());
                }
                else
                {
                    if ((nextUpdate - cr.LastUpdated) < maxAge)
                        updateNow.Add(s.UpdateRates());
                }
            }
            if (updateNow.Count > 0)
                Task.WhenAll(updateNow).RunAsync();
            foreach (var x in sources)
                x.StartUpdating(maxSchedule);
        }
        
        public void Dispose()
        {
            foreach (var x in Sources)
                x.Dispose();
        }

        readonly List<RateSource> Sources;


        readonly ServiceManager Manager;

        const String SystemName = "ExchangeRate";

        public PerfMonitor PerfMon { get; } = new PerfMonitor(SystemName);

        public override string ToString()
        {
            GetSourceRates(out var x);
            return x.S?.ToString();
        }

        /// <summary>
        /// Get all rates
        /// </summary>
        /// <returns></returns>
        RateCache GetSourceRates(out RateSource src)
        {
            RateCache best = null;
            src = null;
            TimeSpan bestTime = TimeSpan.MaxValue;
            var now = DateTime.UtcNow;
            foreach (var x in Sources)
            {
                var r = x.Rates;
                if (r == null)
                    continue;
                var age = now - r.LastUpdated;
                if (age < bestTime)
                {
                    best = r;
                    bestTime = age;
                    src = x;
                }
                if (age < x.Fail)
                    return r;
            }
            return best;
        }

        /// <summary>
        /// Get all rates
        /// </summary>
        /// <returns></returns>
        public RateCache GetRates()
            => GetSourceRates(out var _);

        /// <summary>
        /// Get rates if they have been updated
        /// </summary>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth(Roles.Service)]

        public Rates SyncRates(DateTime newerThan)
        {
            var r = GetRates();
            if (r == null)
                return null;
            if (r.LastUpdated <= newerThan)
                return null;
            return r.Original;
        }

        /// <summary>
        /// The current exchange rates
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth]
        [WebMenuTable(null, "Debug/ExchangeRate/RatesTable", "Current rates", null, "IconCashBill", 1, "Debug,Ops")]
        [WebApiClientCache(10)]
        [WebApiRequestCache(14)]
        public TableData RatesTable(TableDataRequest request)
        {
            request = request ?? new TableDataRequest();
            var comp = "USD";
            Decimal compMag = 1000;


            var c = GetRates();
            if (c == null)
                return null;
            var from = IsoCurrency.TryGet(c.Reference);
            if (from == null) 
                return null;

            var compI = IsoCurrency.TryGet(comp);
            if (compI == null)
                return null;
            comp = compI.Iso4217;


            var data = TableDataTools.Get(request, 15000, c.Original.From.Select(x => new RateRow(c, x, compI, compMag)).Where(x => x.Iso4217 != null));
            var cols = data.Cols;
            if (cols != null)
            {
                data.Title = "Exchange rates";
                var cl = cols.Length;
                var col = cols[RateColumn];
                col.Title = "One " + from.Iso4217;
                col.Desc = "Value of " + from.ToString(1, CurrencyFormatOptions.ApplyThousandsSeparator | CurrencyFormatOptions.Symbol) + " (one " + from.Iso4217 + ") in local currency";
                col = cols[ToColumn];
                col.Title = compMag + " local in " + comp;
                col.Desc = "Value of " + compMag + " local units in " + compI.Name + " (" + comp + ")";
                col = cols[FromColumn];
                col.Title = compMag + " " + comp + " in local";
                col.Desc = "Value of " + compI.ToString(compMag, CurrencyFormatOptions.ApplyThousandsSeparator | CurrencyFormatOptions.Symbol) + " (" + comp + ") in local currency";
            }
            return data;
        }


        /// <summary>
        /// The sources for exchange rates
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth]
        [WebMenuTable(null, "Debug/ExchangeRate/SourceTables", "Sources", null, "IconTableCountry", 2, "Debug,Ops")]
        [WebApiClientCache(10)]
        [WebApiRequestCache(14)]
        public TableData SourcesTable(TableDataRequest request)
        {
            GetSourceRates(out var cs);
            return TableDataTools.Get(request, 15000, Sources.Select(x => new SourceRow(x, cs)));
        }

        /// <summary>
        /// The exchange rates from all sources
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth]
        [WebMenuTable(null, "Debug/ExchangeRate/AllRatesTable", "All rates", null, "IconCashBills", 3, "Debug,Ops")]
        [WebApiClientCache(10)]
        [WebApiRequestCache(14)]
        public TableData AllRatesTable(TableDataRequest request)
        {
            request = request ?? new TableDataRequest();
            var comp = "USD";
            Decimal compMag = 1000;

            var compI = IsoCurrency.TryGet(comp);
            if (compI == null)
                return null;
            comp = compI.Iso4217;

            List<AllRateRow> a = new List<AllRateRow>();
            foreach (var ss in Sources)
            {
                var rates = ss.Rates;
                if (rates != null)
                {
                    var s = rates.Source;
                    var sr = rates.Reference;
                    foreach (var x in rates.Original.From)
                    {
                        var t = new AllRateRow(s, sr, rates, x, compI, compMag);
                        if (t.Iso4217 != null)
                            a.Add(t);
                    }
                }
            }
            var data = TableDataTools.Get(request, 15000, a);
            var cols = data.Cols;
            if (cols != null)
            {
                data.Title = "All exchange rates";
                var cl = cols.Length;
                var col = cols[SRateColumn];
                col.Title = "One reference";
                col.Desc = "Value of one reference unit in local currency";
                col = cols[SToColumn];
                col.Title = compMag + " local in " + comp;
                col.Desc = "Value of " + compMag + " local units in " + compI.Name + " (" + comp + ")";
                col = cols[SFromColumn];
                col.Title = compMag + " " + comp + " in local";
                col.Desc = "Value of " + compI.ToString(compMag, CurrencyFormatOptions.ApplyThousandsSeparator | CurrencyFormatOptions.Symbol) + " (" + comp + ") in local currency";
            }
            return data;
        }

        static readonly int RateColumn = TableDataTools.GetColumnIndex<RateRow>(nameof(RateRow.Rate));
        static readonly int ToColumn = TableDataTools.GetColumnIndex<RateRow>(nameof(RateRow.To));
        static readonly int FromColumn = TableDataTools.GetColumnIndex<RateRow>(nameof(RateRow.From));

        static readonly int SRateColumn = TableDataTools.GetColumnIndex<AllRateRow>(nameof(RateRow.Rate));
        static readonly int SToColumn = TableDataTools.GetColumnIndex<AllRateRow>(nameof(RateRow.To));
        static readonly int SFromColumn = TableDataTools.GetColumnIndex<AllRateRow>(nameof(RateRow.From));


        public IEnumerable<Stats> GetStats()
        {
            foreach (var x in Sources)
            {
                foreach (var t in x.Fails.GetStats(SystemName, String.Join(x.S.Source, "Fails.", '.')))
                    yield return t;
             }
        }
    }

}
