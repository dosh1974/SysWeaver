using System.Globalization;

namespace SysWeaver.ExchangeRate
{
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
        /// The exchange rate multiplier (multiply to get from the currency code to the reference curreny)
        /// </summary>
        public decimal R;
    }


}
