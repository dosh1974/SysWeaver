using System;
using SysWeaver.IsoData;


namespace SysWeaver
{
    public static class AmountExt
    {

        /// <summary>
        /// Get information about the currency part of the amount
        /// </summary>
        /// <param name="amount">The amount</param>
        /// <returns>Currency information or null if unknown</returns>
        public static IsoCurrency CurrencyInfo(this Amount amount)
            => IsoCurrency.TryGet(amount?.Currency);


        /// <summary>
        /// Convert the amount to a string using currency specific formatting.
        /// </summary>
        /// <param name="amount">The amount</param>
        /// <returns>The amount as a string or null if unknown currency is used</returns>
        public static String ToAmountString(this Amount amount)
        {
            var c = CurrencyInfo(amount);
            if (c == null)
                return null;
            return c.ToString(amount.Value, CurrencyFormatOptions.Symbol | CurrencyFormatOptions.ApplyThousandsSeparator | CurrencyFormatOptions.AutomaticRounding);
        }

        /// <summary>
        /// Convert the amount to a string using currency specific formatting.
        /// </summary>
        /// <param name="amount">The amount</param>
        /// <param name="autoRound">If false, always show decimals, even if they are all zeros</param>
        /// <param name="forceRound">If true, always round to whole numbers</param>
        /// <returns>The amount as a string or null if unknown currency is used</returns>
        public static String ToAmountString(this Amount amount, bool autoRound, bool forceRound = false)
        {
            var c = CurrencyInfo(amount);
            if (c == null)
                return null;
            return c.ToString(amount.Value, IsoData.CurrencyFormatOptions.Symbol | CurrencyFormatOptions.ApplyThousandsSeparator 
                | (autoRound ? CurrencyFormatOptions.AutomaticRounding : 0)
                | (forceRound ? CurrencyFormatOptions.ForceRounding : 0)
                );
        }


    }

}
