using System;

namespace SysWeaver.ExchangeRate
{
    /// <summary>
    /// A set of exchange rates from a source, all relative to a reference currency
    /// </summary>
    public sealed class Rates
    {
#if DEBUG
        public override string ToString()
            => string.Join(": ", Reference, " @ ", LastUpdated, " (", From?.Length ?? 0, " currencies) from ", Source);
#endif//DEBUG

        /// <summary>
        /// The source of these rates
        /// </summary>
        public String Source;

        /// <summary>
        /// Three letter ISO 4217 reference currency code
        /// </summary>
        public string Reference;

        /// <summary>
        /// Time stamp when this data was collected
        /// </summary>
        public DateTime LastUpdated;

        /// <summary>
        /// The rates of the currencies, each is the amount of that currency that one unit of the reference currency is worth
        /// </summary>
        public Rate[] From;
    }

}
