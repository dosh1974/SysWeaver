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
                //  Numbers (sorted, filtered and exported as numbers), the client formats them using the currency (iso_data/currencies.js)
                To = compMag * rate;
                From = rate == 0 ? 0 : compMag / rate;
                LocalCurrency = fromi;
            }


            /// <summary>
            /// The value of one reference unit in the local currency (a plain number, as before: the amount columns carry the currency)
            /// </summary>
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

            /// <summary>
            /// The value of a number of local units in the compare currency (the currency format is set per table)
            /// </summary>
            [TableDataCurrency("USD")]
            public readonly decimal To;

            /// <summary>
            /// The value of a number of compare currency units in the local currency (the currency of the next column)
            /// </summary>
            [TableDataCurrency("{1}")]
            public readonly decimal From;

            /// <summary>
            /// The ISO 4217 code of the local currency (the currency of the From amount)
            /// </summary>
            [TableDataHide]
            public readonly String LocalCurrency;

            /// <summary>
            /// The format of an amount column in a currency
            /// </summary>
            /// <param name="iso4217">The ISO 4217 code of the currency</param>
            /// <returns>The column format</returns>
            internal static String CurrencyFormat(String iso4217) => new TableDataCurrencyAttribute(iso4217).Value;


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
