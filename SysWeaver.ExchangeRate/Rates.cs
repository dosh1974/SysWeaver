using System;

namespace SysWeaver.ExchangeRate
{
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
        /// The rates to use for converting from a currency to the reference currency
        /// </summary>
        public Rate[] From;
    }

}
