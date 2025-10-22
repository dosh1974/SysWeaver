using System;
using SysWeaver.Data;
using SysWeaver.IsoData;

namespace SysWeaver.ExchangeRate
{
    public sealed partial class ExchangeRateService
    {

        [TableDataPrimaryKey(nameof(Name))]
        class RateRow
        {
            public RateRow(RateCache rates, Rate r, IsoCurrency to, Decimal compMag)
            {
                var from = IsoCurrency.TryGet(r.I);
                String fromi = r.I;
                if (from == null)
                {
                    Iso4217 = "* " + fromi;
                    Country = "?";
                    Name = fromi + " [Unknown ISO code]";
                }
                else
                {
                    fromi = from.Iso4217;
                    Iso4217 = fromi;
                    Country = from.Country;
                    Name = from.Name;
                    CommonlyUsed = from.CommonlyUsed;
                }
                Rate = r.R;
                var toi = to.Iso4217;
                var rate = rates.GetRate(fromi, toi);
                Updated = rates.LastUpdated;
                To = to.ToString(compMag * rate, CurrencyFormatOptions.ApplyThousandsSeparator | CurrencyFormatOptions.Symbol);
                if (from == null)
                    From = String.Join(' ', r.I, (compMag / rate).ToString("### ### ### ### ##0.00"));
                else
                    From = from.ToString(compMag / rate, CurrencyFormatOptions.ApplyThousandsSeparator | CurrencyFormatOptions.Symbol);
            }



            [TableDataNumber(4)]
            public readonly decimal Rate;

            /// <summary>
            /// The ISO 4217 currency code of the currency
            /// </summary>
            [TableDataIsoCurrency]
            [TableDataKey]
            public readonly String Iso4217;

            /// <summary>
            /// Time when this the rate data was updated
            /// </summary>
            public readonly DateTime Updated;

            public readonly String To;

            public readonly String From;


            /// <summary>
            /// Official name of the currency
            /// </summary>
            [TableDataWikipedia]
            public readonly String Name;

            /// <summary>
            /// The flag of the "biggest" country that is using this currency.
            /// </summary>
            [TableDataIsoCountryImage]
            [TableDataOrder(1)]
            public String Flag => Country;

            /// <summary>
            /// The "biggest" country that is using this currency as ISO 3166 Alpha 2 country code, can be null or empty for special currencies
            /// </summary>
            [TableDataIsoCountry]
            [TableDataOrder(2)]
            [TableDataKey]
            public readonly String Country;

            /// <summary>
            /// Countries / regions where the currency is commonly used
            /// </summary>
            [TableDataGoogleSearch]
            [TableDataOrder(3)]
            public readonly String CommonlyUsed;


        }
    }

}
