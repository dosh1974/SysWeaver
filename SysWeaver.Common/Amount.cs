using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SysWeaver
{

    /// <summary>
    /// Represents an amount (value / currency pair)
    /// </summary>
    public sealed class Amount
    {
        /// <summary>
        /// Format as "CUR value", with space separated thousand groups and up to 7 decimals (invariant culture).
        /// </summary>
        /// <remarks>Intended for display / debugging only, the literal spaces in the format pattern may produce extra padding before the value.</remarks>
        /// <returns>A display string.</returns>
        public override string ToString() => String.Join(' ', Currency, Value.ToString("### ### ### ### ### ##0.#######", CultureInfo.InvariantCulture));

        /// <summary>
        /// The value part of the amount
        /// </summary>
        public Decimal Value;

        /// <summary>
        /// The ISO-4217 currency code
        /// </summary>
        public String Currency;

        /// <summary>
        /// Create an empty amount (zero value, no currency), used by serializers.
        /// </summary>
        public Amount()
        {
        }

        /// <summary>
        /// Create an amount.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="currencyCode">The ISO-4217 currency code, ex: "USD" (not validated).</param>
        public Amount(Decimal value, String currencyCode)
        {
            Value = value;
            Currency = currencyCode;
        }
    }

}
 