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
        public override string ToString() => String.Join(' ', Currency, Value.ToString("### ### ### ### ### ##0.#######", CultureInfo.InvariantCulture));

        /// <summary>
        /// The value part of the amount
        /// </summary>
        public Decimal Value;

        /// <summary>
        /// The ISO-4217 currency code
        /// </summary>
        public String Currency;
    }

}
