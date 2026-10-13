using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

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
            foreach (var y in oldToNewMap.Nullable())
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
            //  Upper case ASCII codes (the common case) are looked up as is (FastToUpper returns them), others are upper cased on the stack
            if (!(IsUpperAscii(from) && IsUpperAscii(to)))
            {
                if ((from.Length <= MaxStackLength) && (to.Length <= MaxStackLength))
                    return GetRateUpperCased(from, to, true);
                from = from.FastToUpper();
                to = to.FastToUpper();
            }
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
            //  Upper case ASCII codes (the common case) are looked up as is (FastToUpper returns them), others are upper cased on the stack
            if (!(IsUpperAscii(from) && IsUpperAscii(to)))
            {
                if ((from.Length <= MaxStackLength) && (to.Length <= MaxStackLength))
                    return GetRateUpperCased(from, to, false);
                from = from.FastToUpper();
                to = to.FastToUpper();
            }
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

        /// <summary>
        /// Currency codes up to this length are upper cased on the stack
        /// </summary>
        const int MaxStackLength = 32;

        /// <summary>
        /// Upper case chars (the same chars as FastToUpper), ASCII using SIMD
        /// </summary>
        static void ToUpper(ReadOnlySpan<Char> src, Span<Char> dst)
        {
            if (System.Text.Ascii.ToUpper(src, dst, out _) != System.Buffers.OperationStatus.Done)
                src.ToUpperInvariant(dst);
        }

        /// <summary>
        /// True if FastToUpper returns the code itself (upper case ASCII), false for long codes (they are checked by FastToUpper)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsUpperAscii(String s)
        {
            //  Null (throws like FastToUpper on the other path) and long codes take the other path
            if ((s is null) || (s.Length > 8))
                return false;
            var c = s.AsSpan();
            foreach (uint x in c)
                if (((x - 'a') < 26) || (x >= 0x80))
                    return false;
            return true;
        }

        /// <summary>
        /// GetRate / GetRateNoThrow for codes that FastToUpper changes: upper cased on the stack (the same chars as FastToUpper), no allocation unless it throws.
        /// Not inlined, so that the common path has no stack allocation.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        Decimal GetRateUpperCased(String from, String to, bool throwIfUnknown)
        {
            Span<Char> f = stackalloc Char[from.Length];
            Span<Char> t = stackalloc Char[to.Length];
            ToUpper(from, f);
            ToUpper(to, t);
            if (f.SequenceEqual(t))
                return 1;
            var rs = Rates;
            if (!rs.TryGetValueSpan(f, out var tr))
                return throwIfUnknown ? throw new Exception("Can't find exchange rate for " + f.ToString().ToQuoted()) : -1;
            if (!rs.TryGetValueSpan(t, out var fr))
                return throwIfUnknown ? throw new Exception("Can't find exchange rate for " + t.ToString().ToQuoted()) : -1;
            fr /= tr;
            return fr;
        }

    }


}
