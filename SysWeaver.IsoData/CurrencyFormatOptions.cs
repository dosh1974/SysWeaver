using System;

namespace SysWeaver.IsoData
{
    /// <summary>
    /// Formatting options when converting an amount value to a displayable string
    /// </summary>
    [Flags]
    public enum CurrencyFormatOptions
    {
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
        /// Uses symbol formatting, ex: "$100.00", "100.00 kr" or "MXV 100.00"
        /// </summary>
        Symbol = 3,
        /// <summary>
        /// Rounds the value to the nearest integer (removing under units)
        /// </summary>
        ForceRounding = 4,
        /// <summary>
        /// Adds the specified thousands separfator
        /// </summary>
        ApplyThousandsSeparator = 8,
        /// <summary>
        /// Removes any underunit zeros, ex: "$100.00" => "$100"
        /// </summary>
        AutomaticRounding = 16,
        /// <summary>
        /// The default formatting option
        /// </summary>
        Default = IsoPrefix | ApplyThousandsSeparator,
    }

}
