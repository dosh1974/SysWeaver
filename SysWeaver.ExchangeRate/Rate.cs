using System.Globalization;

namespace SysWeaver.ExchangeRate
{
    /// <summary>
    /// The exchange rate of one currency, relative to the reference currency of the <see cref="Rates"/>
    /// </summary>
    public sealed class Rate
    {
#if DEBUG
        public override string ToString()
            => string.Join(": ", I, R.ToString("0.######", CultureInfo.InvariantCulture));
#endif//DEBUG

        /// <summary>
        /// Three letter ISO 4217 currency code
        /// </summary>
        public string I;

        /// <summary>
        /// The exchange rate: the amount of this currency that one unit of the reference currency is worth (multiply an amount of the reference currency with it to get an amount of this currency)
        /// </summary>
        public decimal R;
    }


}
