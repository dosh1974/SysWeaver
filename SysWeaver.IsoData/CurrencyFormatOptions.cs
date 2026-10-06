using System;

namespace SysWeaver.IsoData
{
    /// <summary>
    /// Formatting options when converting an amount value to a displayable string, see <see cref="IsoCurrency.ToString(decimal, CurrencyFormatOptions, string, string)"/>.
    /// </summary>
    /// <remarks>
    /// The two lowest bits select the currency style: <see cref="IsoPrefix"/>, <see cref="IsoSuffix"/> or <see cref="Symbol"/> (both bits), or none.
    /// The remaining values are independent flags.
    /// </remarks>
    [Flags]
    public enum CurrencyFormatOptions
    {
        /// <summary>
        /// Only the value is formatted (no currency, no thousands separator, under units are always shown when the currency has any).
        /// </summary>
        None = 0,
        /// <summary>
        /// Adds a ISO 4217 currency code as a prefix, ex: "USD 100.00"
        /// </summary>
        IsoPrefix = 1,
        /// <summary>
        /// Adds a ISO 4217 currency code as a suffix, ex: "100.00 USD"
        /// </summary>
        IsoSuffix = 2,
        /// <summary>
        /// Uses symbol formatting (<see cref="IsoCurrency.SymbolPrefix"/> + value + <see cref="IsoCurrency.SymbolSuffix"/>), ex: "$ 100.00", "100.00 kr" or "MXV 100.00" (ISO code prefix when the currency has no symbol).
        /// Note that this value is the combination of <see cref="IsoPrefix"/> and <see cref="IsoSuffix"/>.
        /// </summary>
        Symbol = 3,
        /// <summary>
        /// Rounds the value to the nearest integer (removing under units), using banker's rounding (midpoints round to even).
        /// </summary>
        ForceRounding = 4,
        /// <summary>
        /// Groups the integer part in groups of three digits using the currency's <see cref="IsoCurrency.ThousandsSeparator"/> (or the supplied override).
        /// </summary>
        ApplyThousandsSeparator = 8,
        /// <summary>
        /// Omits the under units when they round to zero, ex: "$ 100.00" => "$ 100" ("$ 100.50" is kept as is).
        /// </summary>
        AutomaticRounding = 16,
        /// <summary>
        /// The default formatting option: ISO code prefix and thousands separator, ex: "USD 1 234.50".
        /// </summary>
        Default = IsoPrefix | ApplyThousandsSeparator,
    }

}
