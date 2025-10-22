using System;
using System.Collections.Generic;

namespace SysWeaver.ExchangeRate
{

    public sealed class RateCache
    {
#if DEBUG
        public override string ToString()
            => string.Join(": ", Reference, " @ ", LastUpdated, " (",Count, " currencies) from ", Source);
#endif//DEBUG

        public RateCache(Rates rates, IReadOnlyDictionary<String, String> oldToNewMap)
        {
            Original = rates;
            Source = rates.Source;
            Reference = rates.Reference.FastToUpper();
            LastUpdated = rates.LastUpdated;
            var t = new Dictionary<String, Decimal>(StringComparer.Ordinal);
            foreach (var x in rates.From.Nullable())
                t[x.I.FastToUpper()] = x.R;
            foreach (var y in oldToNewMap?.Nullable())
            {
                var old = y.Key.FastToUpper();
                var nw = y.Value.FastToUpper();
                if (!t.TryGetValue(nw, out var e))
                    throw new ArgumentException("The currency " + nw.ToQuoted() + " does not exist!", nameof(oldToNewMap));
                t[old] = e;
            }
            Count = t.Count;
            Rates = t.Freeze();
        }


        internal readonly Rates Original;

        /// <summary>
        /// The source of these rates
        /// </summary>
        public readonly String Source;

        /// <summary>
        /// Three letter ISO 4217 reference currency code
        /// </summary>
        public readonly string Reference;

        /// <summary>
        /// Time stamp when this data was collected
        /// </summary>
        public readonly DateTime LastUpdated;

        /// <summary>
        /// Number of rates
        /// </summary>
        public readonly int Count;


        /// <summary>
        /// Get the exchange rate (value to multiply with) to get from one currency to another.
        /// </summary>
        /// <param name="from">Convert from this currency (as a three letter ISO 4217 currency code)</param>
        /// <param name="to">Convert to this currency (as a three letter ISO 4217 currency code)</param>
        /// <returns>The value to multiple with</returns>
        /// <exception cref="Exception"></exception>
        public Decimal GetRate(String from, String to)
        {
            from = from.FastToUpper();
            to = to.FastToUpper();
            if (from == to)
                return 1;
            var r = Rates;
            if (!r.TryGetValue(from, out var tr))
                throw new Exception("Can't find exchange rate for " + from.ToQuoted());
            if (!r.TryGetValue(to, out var fr))
                throw new Exception("Can't find exchange rate for " + to.ToQuoted());
            fr /= tr;
            return fr;
        }

        /// <summary>
        /// Get the exchange rate (value to multiply with) to get from one currency to another.
        /// </summary>
        /// <param name="from">Convert from this currency (as a three letter ISO 4217 currency code)</param>
        /// <param name="to">Convert to this currency (as a three letter ISO 4217 currency code)</param>
        /// <returns>The value to multiple with or -1 if the from or to currency is unknown</returns>
        public Decimal GetRateNoThrow(String from, String to)
        {
            from = from.FastToUpper();
            to = to.FastToUpper();
            if (from == to)
                return 1;
            var r = Rates;
            if (!r.TryGetValue(from, out var tr))
                return -1;
            if (!r.TryGetValue(to, out var fr))
                return -1;
            fr /= tr;
            return fr;
        }


        readonly IReadOnlyDictionary<String, Decimal> Rates;

    }


}
