using System;
using SysWeaver.Data;
using SysWeaver.IsoData;

namespace SysWeaver.ExchangeRate
{
    public sealed partial class ExchangeRateService
    {
        sealed class AllRateRow : RateRow
        {
            public AllRateRow(String source, String srcRef, RateCache rates, Rate r, IsoCurrency to, Decimal compMag) 
                : base(rates, r, to, compMag)
            {
                Source = source;
                Reference = srcRef;
            }

            /// <summary>
            /// The source of this exchange rate
            /// </summary>
            public readonly String Source;

            /// <summary>
            /// The source's reference currency
            /// </summary>
            [TableDataIsoCurrency]
            public readonly String Reference;
        }
    }

}
