using System;
using SysWeaver.IsoData;


namespace SysWeaver
{
    /// <summary>
    /// Extension methods that combine an <see cref="Amount"/> with the built-in <see cref="IsoCurrency"/> data,
    /// e.g. to format money using currency specific symbols and separators.
    /// </summary>
    public static class AmountExt
    {

        /// <summary>
        /// Get information about the currency part of the amount.
        /// </summary>
        /// <param name="amount">The amount, may be null.</param>
        /// <returns>Currency information, or null if <paramref name="amount"/> is null or its currency code is unknown (lookup is case insensitive).</returns>
        public static IsoCurrency CurrencyInfo(this Amount amount)
            => IsoCurrency.TryGet(amount?.Currency);


        /// <summary>
        /// Convert the amount to a string using currency specific formatting, i.e. the currency symbol (or ISO code if the currency has no symbol),
        /// the currency's thousands separator and automatic rounding (under units are omitted when they round to zero).
        /// Ex: "$ 1 234.50", "$ 100", "100 kr".
        /// </summary>
        /// <param name="amount">The amount, may be null.</param>
        /// <returns>The amount as a string, or null if <paramref name="amount"/> is null or an unknown currency is used.</returns>
        /// <remarks>Uses <see cref="IsoCurrency.ToString(decimal, CurrencyFormatOptions, string, string)"/> with <see cref="CurrencyFormatOptions.Symbol"/>,
        /// <see cref="CurrencyFormatOptions.ApplyThousandsSeparator"/> and <see cref="CurrencyFormatOptions.AutomaticRounding"/>.</remarks>
        public static String ToAmountString(this Amount amount)
        {
            var c = CurrencyInfo(amount);
            if (c == null)
                return null;
            return c.ToString(amount.Value, CurrencyFormatOptions.Symbol | CurrencyFormatOptions.ApplyThousandsSeparator | CurrencyFormatOptions.AutomaticRounding);
        }

        /// <summary>
        /// Convert the amount to a string using currency specific formatting (symbol and thousands separator).
        /// </summary>
        /// <param name="amount">The amount, may be null.</param>
        /// <param name="autoRound">If true, under units are omitted when they round to zero (<see cref="CurrencyFormatOptions.AutomaticRounding"/>).
        /// If false, always show decimals, even if they are all zeros.</param>
        /// <param name="forceRound">If true, always round to whole units (<see cref="CurrencyFormatOptions.ForceRounding"/>, uses banker's rounding).</param>
        /// <returns>The amount as a string, or null if <paramref name="amount"/> is null or an unknown currency is used.</returns>
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
