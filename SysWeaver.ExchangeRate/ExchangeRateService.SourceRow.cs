using System;
using SysWeaver.Data;

namespace SysWeaver.ExchangeRate
{
    public sealed partial class ExchangeRateService
    {
        [TableDataPrimaryKey(nameof(Name))]

        sealed class SourceRow
        {
            public SourceRow(RateSource s, RateSource current)
            {
                var ss = s.S;
                Name = ss.Source;
                var r = s.Rates;
                if (r != null)
                {
                    Updated = r.LastUpdated;
                    Reference = r.Reference;
                    Count = r.Count;
                    Name = r.Source;
                }
                var f = s.Fails;
                var fc = f.Count;
                if (fc > 0)
                {
                    Fails = fc;
                    LastFailAt = new DateTime(f.LastTime, DateTimeKind.Utc);
                    LastFail = f.LastException?.Message;
                }
                NextUpdate = new DateTime(s.NextUpdateTick, DateTimeKind.Utc);
                IsCurrent = s == current;
            }

            /// <summary>
            /// Name of the exchange rate source
            /// </summary>
            public readonly String Name;

            /// <summary>
            /// True if the is the current exchange rate source (the one in use)
            /// </summary>
            public readonly bool IsCurrent;

            /// <summary>
            /// The time when the last update from this source was made
            /// </summary>
            public readonly DateTime Updated;

            /// <summary>
            /// The ISO 4217 currency code used as reference currency
            /// </summary>
            [TableDataIsoCurrency]
            public readonly String Reference;

            /// <summary>
            /// Number of currencies
            /// </summary>
            public readonly int Count;

            /// <summary>
            /// When the next update in scheduled
            /// </summary>
            public readonly DateTime NextUpdate;

            /// <summary>
            /// Number of fails
            /// </summary>
            public readonly long Fails;

            /// <summary>
            /// When the last fail happened
            /// </summary>
            public readonly DateTime LastFailAt;

            /// <summary>
            /// Last failure
            /// </summary>
            public readonly String LastFail;
        }
    }

}
