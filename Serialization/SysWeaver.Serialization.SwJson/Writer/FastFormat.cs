using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SysWeaver.Serialization.SwJson.Writer
{
    /// <summary>
    /// Fast formatting of values to UTF8 (json), writing to a pointer.
    /// The caller must make sure that there is room for the output (see each method).
    /// The output is identical to the .NET formatting that the JsonWriter used before (verified by the tests).
    /// </summary>
    [SkipLocalsInit]
    static unsafe class FastFormat
    {
        #region Integers

        /// <summary>
        /// "00", "01", .. "99" as little endian ushort's
        /// </summary>
        static readonly ushort[] DigitPairs = CreateDigitPairs();

        static ushort[] CreateDigitPairs()
        {
            var t = new ushort[100];
            for (int i = 0; i < 100; ++i)
                t[i] = (ushort)(('0' + i / 10) | (('0' + i % 10) << 8));
            return t;
        }

        static readonly ulong[] PowersOf10 = CreatePowersOf10();

        static ulong[] CreatePowersOf10()
        {
            var t = new ulong[20];
            ulong v = 1;
            for (int i = 0; i < 20; ++i)
            {
                t[i] = v;
                v *= 10;
            }
            return t;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ushort Pair(uint value) => Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(DigitPairs), (nint)value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void WritePair(Byte* d, uint value) => Unsafe.WriteUnaligned(d, Pair(value));

        /// <summary>
        /// The number of decimal digits (1 for 0)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CountDigits(ulong value)
        {
            var v = value | 1;
            // floor(log10(2^(bits + 1))) approximation, then adjust
            int t = ((BitOperations.Log2(v) + 1) * 1233) >> 12;
            return t + 1 - (v < Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(PowersOf10), t) ? 1 : 0);
        }

        /// <summary>
        /// Write an unsigned integer, max 10 bytes
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Byte* WriteUInt32(Byte* d, uint value)
        {
            if (value < 10)
            {
                *d = (Byte)('0' + value);
                return d + 1;
            }
            if (value < 100)
            {
                WritePair(d, value);
                return d + 2;
            }
            return WriteUInt32Slow(d, value);
        }

        static Byte* WriteUInt32Slow(Byte* d, uint value)
        {
            var end = d + CountDigits(value);
            var p = end;
            while (value >= 100)
            {
                var q = value / 100;
                p -= 2;
                WritePair(p, value - q * 100);
                value = q;
            }
            if (value >= 10)
                WritePair(p - 2, value);
            else
                p[-1] = (Byte)('0' + value);
            return end;
        }

        /// <summary>
        /// Write an unsigned integer, max 20 bytes
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Byte* WriteUInt64(Byte* d, ulong value)
        {
            if (value <= uint.MaxValue)
                return WriteUInt32(d, (uint)value);
            return WriteUInt64Slow(d, value);
        }

        static Byte* WriteUInt64Slow(Byte* d, ulong value)
        {
            var end = d + CountDigits(value);
            var p = end;
            // Use 64 bit division until the value fits 32 bits
            while (value > uint.MaxValue)
            {
                var q = value / 100;
                p -= 2;
                WritePair(p, (uint)(value - q * 100));
                value = q;
            }
            var v = (uint)value;
            while (v >= 100)
            {
                var q = v / 100;
                p -= 2;
                WritePair(p, v - q * 100);
                v = q;
            }
            if (v >= 10)
                WritePair(p - 2, v);
            else
                p[-1] = (Byte)('0' + v);
            return end;
        }

        /// <summary>
        /// Write a signed integer, max 11 bytes
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Byte* WriteInt32(Byte* d, int value)
        {
            if (value < 0)
            {
                *d = (Byte)'-';
                return WriteUInt32(d + 1, 0u - (uint)value);
            }
            return WriteUInt32(d, (uint)value);
        }

        /// <summary>
        /// Write a signed integer, max 20 bytes
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Byte* WriteInt64(Byte* d, long value)
        {
            if (value < 0)
            {
                *d = (Byte)'-';
                return WriteUInt64(d + 1, 0ul - (ulong)value);
            }
            return WriteUInt64(d, (ulong)value);
        }

        /// <summary>
        /// Write exactly "count" digits (zero padded), value must be less than 10^count
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void WriteDigitsPadded(Byte* d, uint value, int count)
        {
            var p = d + count;
            while (count >= 2)
            {
                var q = value / 100;
                p -= 2;
                WritePair(p, value - q * 100);
                value = q;
                count -= 2;
            }
            if (count > 0)
                p[-1] = (Byte)('0' + value);
        }

        #endregion//Integers

        #region Floating point

        static readonly double[] Pow10Double = CreatePow10Double();

        static double[] CreatePow10Double()
        {
            var t = new double[23];
            for (int i = 0; i < t.Length; ++i)
                t[i] = double.Parse("1e" + i, System.Globalization.CultureInfo.InvariantCulture);
            return t;
        }

        static readonly float[] Pow10Single = [1e0f, 1e1f, 1e2f, 1e3f, 1e4f, 1e5f, 1e6f, 1e7f, 1e8f, 1e9f, 1e10f];

        // 2^50, scaled values must be below this
        const double MaxScaledDouble = 1125899906842624.0;
        // 2^21
        const float MaxScaledSingle = 2097152.0f;

        /// <summary>
        /// Writes a (finite, non integral) double with few decimals, like 1234.56 (the common case for "real world" values).
        /// Returns null if the value isn't handled, max 32 bytes.
        /// The output is identical to TryFormat("r"), the shortest round trippable representation:
        /// if n / 10^k == value (n and 10^k are exactly representable, so the division is correctly rounded),
        /// the decimal string n*10^-k parses to value. With the scaled value below 2^50, the error of value * 10^k is below 1/8,
        /// so any shorter round trippable string would have been found with a smaller k, and the digits for a k are unique.
        /// 0.001 &lt;= |value| &lt; 10^15 is always written without an exponent by "r".
        /// </summary>
        public static Byte* TryWriteShortDouble(Byte* d, double value)
        {
            var a = Math.Abs(value);
            if (!((a >= 1e-3) && (a < 1e15)))
                return null;
            ref var pow = ref MemoryMarshal.GetArrayDataReference(Pow10Double);
            for (int k = 1; k <= 17; ++k)
            {
                var p = Unsafe.Add(ref pow, k);
                var s = a * p;
                if (s >= MaxScaledDouble)
                    return null;
                var n = Math.Round(s);
                if ((n / p) != a)
                    continue;
                if (value < 0)
                {
                    *d = (Byte)'-';
                    ++d;
                }
                return WriteScaled(d, (ulong)n, k);
            }
            return null;
        }

        /// <summary>
        /// Like TryWriteShortDouble but for a float, max 32 bytes
        /// </summary>
        public static Byte* TryWriteShortSingle(Byte* d, float value)
        {
            var a = MathF.Abs(value);
            if (!((a >= 1e-3f) && (a < 1e6f)))
                return null;
            ref var pow = ref MemoryMarshal.GetArrayDataReference(Pow10Single);
            for (int k = 1; k <= 10; ++k)
            {
                var p = Unsafe.Add(ref pow, k);
                var s = a * p;
                if (s >= MaxScaledSingle)
                    return null;
                var n = MathF.Round(s);
                if ((n / p) != a)
                    continue;
                if (value < 0)
                {
                    *d = (Byte)'-';
                    ++d;
                }
                return WriteScaled(d, (ulong)n, k);
            }
            return null;
        }

        /// <summary>
        /// Write n*10^-scale (with scale > 0) as a decimal number, like "12.34" or "0.0012"
        /// </summary>
        static Byte* WriteScaled(Byte* d, ulong n, int scale)
        {
            var pow = PowersOf10[scale];
            var ip = n / pow;
            var fp = n - ip * pow;
            d = WriteUInt64(d, ip);
            *d = (Byte)'.';
            ++d;
            // Fraction, zero padded to "scale" digits
            var fd = CountDigits(fp);
            for (int i = scale - fd; i > 0; --i)
            {
                *d = (Byte)'0';
                ++d;
            }
            return WriteUInt64(d, fp);
        }

        /// <summary>
        /// Write a decimal (that isn't an integer) with a 64 bit mantissa, identical to TryFormat("r").
        /// Returns null if the value isn't handled, max 32 bytes.
        /// </summary>
        public static Byte* TryWriteDecimal(Byte* d, decimal value)
        {
            Span<int> bits = stackalloc int[4];
            decimal.GetBits(value, bits);
            if (bits[2] != 0)
                return null;
            var flags = bits[3];
            var scale = (flags >> 16) & 0xff;
            if (scale == 0)
                return null;
            var n = ((ulong)(uint)bits[1] << 32) | (uint)bits[0];
            if (n == 0)
                return null;
            if (flags < 0)
            {
                *d = (Byte)'-';
                ++d;
            }
            var digits = CountDigits(n);
            if (digits <= scale)
            {
                // 0.000ddd
                *d = (Byte)'0';
                ++d;
                *d = (Byte)'.';
                ++d;
                for (int i = scale - digits; i > 0; --i)
                {
                    *d = (Byte)'0';
                    ++d;
                }
                return WriteUInt64(d, n);
            }
            // Fraction digits are kept as is (including trailing zeros)
            var pow = PowersOf10[scale];
            var ip = n / pow;
            var fp = n - ip * pow;
            d = WriteUInt64(d, ip);
            *d = (Byte)'.';
            ++d;
            for (int i = scale - CountDigits(fp); i > 0; --i)
            {
                *d = (Byte)'0';
                ++d;
            }
            return WriteUInt64(d, fp);
        }

        #endregion//Floating point

        #region Date and time

        /// <summary>
        /// Write the fraction of a second (0 - 9 999 999 ticks) with trailing zeros removed, nothing is written for 0
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Byte* WriteTrimmedFraction(Byte* d, uint fraction)
        {
            if (fraction == 0)
                return d;
            *d = (Byte)'.';
            ++d;
            int count = 7;
            while ((fraction % 10) == 0)
            {
                fraction /= 10;
                --count;
            }
            WriteDigitsPadded(d, fraction, count);
            return d + count;
        }

        /// <summary>
        /// yyyy-MM-ddTHH:mm:ss (19 bytes) followed by the trimmed fraction (max 8 bytes)
        /// </summary>
        static Byte* WriteDateTimeCore(Byte* d, DateTime value)
        {
            value.Deconstruct(out int year, out int month, out int day);
            var ticks = (ulong)value.Ticks % TimeSpan.TicksPerDay;
            var seconds = (uint)(ticks / TimeSpan.TicksPerSecond);
            var fraction = (uint)(ticks - seconds * (ulong)TimeSpan.TicksPerSecond);
            var minutes = seconds / 60;
            seconds -= minutes * 60;
            var hours = minutes / 60;
            minutes -= hours * 60;
            var y = (uint)year;
            var yh = y / 100;
            WritePair(d, yh);
            WritePair(d + 2, y - yh * 100);
            d[4] = (Byte)'-';
            WritePair(d + 5, (uint)month);
            d[7] = (Byte)'-';
            WritePair(d + 8, (uint)day);
            d[10] = (Byte)'T';
            WritePair(d + 11, hours);
            d[13] = (Byte)':';
            WritePair(d + 14, minutes);
            d[16] = (Byte)':';
            WritePair(d + 17, seconds);
            return WriteTrimmedFraction(d + 19, fraction);
        }

        /// <summary>
        /// Like TryFormat("o") with trailing fraction zeros removed, for Utc and Unspecified times.
        /// Returns null for local times (the offset needs the local time zone). Max 28 bytes.
        /// </summary>
        public static Byte* TryWriteDateTime(Byte* d, DateTime value)
        {
            var kind = value.Kind;
            if (kind == DateTimeKind.Local)
                return null;
            d = WriteDateTimeCore(d, value);
            if (kind == DateTimeKind.Utc)
            {
                *d = (Byte)'Z';
                ++d;
            }
            return d;
        }

        /// <summary>
        /// Like TryFormat("o") with trailing fraction zeros removed, max 33 bytes
        /// </summary>
        public static Byte* WriteDateTimeOffset(Byte* d, DateTimeOffset value)
        {
            d = WriteDateTimeCore(d, value.DateTime);
            var offset = value.Offset.Ticks / TimeSpan.TicksPerMinute;
            if (offset < 0)
            {
                *d = (Byte)'-';
                offset = -offset;
            }
            else
            {
                *d = (Byte)'+';
            }
            var o = (uint)offset;
            var h = o / 60;
            WritePair(d + 1, h);
            d[3] = (Byte)':';
            WritePair(d + 4, o - h * 60);
            return d + 6;
        }

        /// <summary>
        /// Like TryFormat("o") for a DateOnly: yyyy-MM-dd (10 bytes)
        /// </summary>
        public static Byte* WriteDateOnly(Byte* d, DateOnly value)
        {
            value.Deconstruct(out int year, out int month, out int day);
            var y = (uint)year;
            var yh = y / 100;
            WritePair(d, yh);
            WritePair(d + 2, y - yh * 100);
            d[4] = (Byte)'-';
            WritePair(d + 5, (uint)month);
            d[7] = (Byte)'-';
            WritePair(d + 8, (uint)day);
            return d + 10;
        }

        /// <summary>
        /// HH:mm:ss (8 bytes) followed by the trimmed fraction (max 8 bytes)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Byte* WriteTimeOfDay(Byte* d, ulong ticks)
        {
            var seconds = (uint)(ticks / TimeSpan.TicksPerSecond);
            var fraction = (uint)(ticks - seconds * (ulong)TimeSpan.TicksPerSecond);
            var minutes = seconds / 60;
            seconds -= minutes * 60;
            var hours = minutes / 60;
            minutes -= hours * 60;
            WritePair(d, hours);
            d[2] = (Byte)':';
            WritePair(d + 3, minutes);
            d[5] = (Byte)':';
            WritePair(d + 6, seconds);
            return WriteTrimmedFraction(d + 8, fraction);
        }

        /// <summary>
        /// Like TryFormat("o") for a TimeOnly with trailing fraction zeros removed, max 16 bytes
        /// </summary>
        public static Byte* WriteTimeOnly(Byte* d, TimeOnly value) => WriteTimeOfDay(d, (ulong)value.Ticks);

        /// <summary>
        /// Like TryFormat("c") for a TimeSpan with trailing fraction zeros removed: [-][d.]hh:mm:ss[.fffffff], max 26 bytes
        /// </summary>
        public static Byte* WriteTimeSpan(Byte* d, TimeSpan value)
        {
            var t = value.Ticks;
            ulong ticks;
            if (t < 0)
            {
                *d = (Byte)'-';
                ++d;
                ticks = 0ul - (ulong)t;
            }
            else
            {
                ticks = (ulong)t;
            }
            var days = ticks / TimeSpan.TicksPerDay;
            if (days != 0)
            {
                d = WriteUInt64(d, days);
                *d = (Byte)'.';
                ++d;
                ticks -= days * TimeSpan.TicksPerDay;
            }
            return WriteTimeOfDay(d, ticks);
        }

        #endregion//Date and time

        #region Strings

        static readonly Byte[] HexDigits = "0123456789abcdef"u8.ToArray();

        /// <summary>
        /// The escape char for '\b' '\t' '\n' '\f' '\r' '"' and '\\', 1 for other chars that must be escaped (as \u00XX), 0 if no escape is needed
        /// </summary>
        public static readonly Byte[] Escapes = CreateEscapes();

        static Byte[] CreateEscapes()
        {
            var t = new Byte[128];
            for (int i = 0; i < 32; ++i)
                t[i] = 1;
            t['\b'] = (Byte)'b';
            t['\t'] = (Byte)'t';
            t['\n'] = (Byte)'n';
            t['\f'] = (Byte)'f';
            t['\r'] = (Byte)'r';
            t['"'] = (Byte)'"';
            t['\\'] = (Byte)'\\';
            return t;
        }

        /// <summary>
        /// True if the char can't be copied as is (it must be escaped or it isn't ascii)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsSpecial(uint c, ref Byte esc) => (c >= 128) || (Unsafe.Add(ref esc, (nint)c) != 0);

        /// <summary>
        /// A mask with a bit set for every char (of 16) that can't be copied as is (it must be escaped or it isn't ascii)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static uint SpecialMask(Vector128<ushort> a, Vector128<ushort> b, out Vector128<Byte> narrowed)
        {
            var c20 = Vector128.Create((ushort)0x20);
            var c5f = Vector128.Create((ushort)0x5f);
            var quote = Vector128.Create((ushort)'"');
            var backslash = Vector128.Create((ushort)'\\');
            // c < 0x20 or c > 0x7f (unsigned wrap around), '"' or '\\'
            var sa = Vector128.GreaterThan(a - c20, c5f) | Vector128.Equals(a, quote) | Vector128.Equals(a, backslash);
            var sb = Vector128.GreaterThan(b - c20, c5f) | Vector128.Equals(b, quote) | Vector128.Equals(b, backslash);
            narrowed = Vector128.Narrow(a, b);
            return Vector128.Narrow(sa, sb).ExtractMostSignificantBits();
        }

        /// <summary>
        /// Make room for a \u00XX escape (6 bytes, 3 are ensured) and the rest of the string, returns the new write position (the buffer may have been replaced)
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        static Byte* ReserveEscape(ref BufferWriter w, int offset, int charsLeft)
        {
            w.Offset = offset;
            w.Ensure(charsLeft * 3 + 16);
            return w.DataPtr + offset;
        }

        /// <summary>
        /// Write a json string (with quotes).
        /// Max 3 bytes per char + 64 must be ensured before calling this, a \u00XX escape (6 bytes) ensures more space as needed.
        /// Lone surrogates are written as U+FFFD (the replacement char).
        /// </summary>
        public static void WriteString(ref BufferWriter w, String value)
        {
            var l = value.Length;
            var org = w.DataPtr;
            var d = org + w.Offset;
            *d = (Byte)'"';
            ++d;
            ref var esc = ref MemoryMarshal.GetArrayDataReference(Escapes);
            fixed (char* srcVal = value)
            {
                var src = srcVal;
                var end = srcVal + l;
                var vend = end - 16;
                uint x;
                for (; ; )
                {
                    if (Vector128.IsHardwareAccelerated)
                    {
                        while (src <= vend)
                        {
                            var mask = SpecialMask(Vector128.Load((ushort*)src), Vector128.Load((ushort*)src + 8), out var narrowed);
                            // All 16 bytes are stored (there is room, at least 3 bytes per char are ensured), only the ones before the first special char are kept
                            narrowed.Store(d);
                            if (mask == 0)
                            {
                                src += 16;
                                d += 16;
                                continue;
                            }
                            var first = BitOperations.TrailingZeroCount(mask);
                            src += first;
                            d += first;
                            goto Special;
                        }
                    }
                    while (src < end)
                    {
                        x = *src;
                        if (IsSpecial(x, ref esc))
                            goto Special;
                        *d = (Byte)x;
                        ++d;
                        ++src;
                    }
                    break;
                Special:
                    // A char that can't be copied as is: an escape or a non ascii char (as UTF8)
                    x = *src;
                    ++src;
                    if (x < 0x80)
                    {
                        var e = Unsafe.Add(ref esc, (nint)x);
                        if (e != 1)
                        {
                            d[0] = (Byte)'\\';
                            d[1] = e;
                            d += 2;
                        }
                        else
                        {
                            // Usually there is room already (only call Ensure when needed)
                            if (((d - org) + (end - src) * 3 + 16) > w.Capacity)
                            {
                                d = ReserveEscape(ref w, (int)(d - org), (int)(end - src));
                                org = w.DataPtr;
                            }
                            ref var h = ref MemoryMarshal.GetArrayDataReference(HexDigits);
                            d[0] = (Byte)'\\';
                            d[1] = (Byte)'u';
                            d[2] = (Byte)'0';
                            d[3] = (Byte)'0';
                            d[4] = Unsafe.Add(ref h, (nint)(x >> 4));
                            d[5] = Unsafe.Add(ref h, (nint)(x & 0xf));
                            d += 6;
                        }
                    }
                    else if (x < 0x800)
                    {
                        d[0] = (Byte)((x >> 6) | 0xc0);
                        d[1] = (Byte)((x & 0x3f) | 0x80);
                        d += 2;
                    }
                    else if ((x - 0xd800) >= 0x800)
                    {
                        // Not a surrogate
                        d[0] = (Byte)((x >> 12) | 0xe0);
                        d[1] = (Byte)(((x >> 6) & 0x3f) | 0x80);
                        d[2] = (Byte)((x & 0x3f) | 0x80);
                        d += 3;
                    }
                    else if ((x <= 0xdbff) && (src < end) && ((uint)(*src - 0xdc00) < 0x400))
                    {
                        // A surrogate pair (4 bytes for 2 chars)
                        x = ((x - 0xd800) << 10) + ((uint)*src - 0xdc00) + 0x10000;
                        ++src;
                        d[0] = (Byte)((x >> 18) | 0xf0);
                        d[1] = (Byte)(((x >> 12) & 0x3f) | 0x80);
                        d[2] = (Byte)(((x >> 6) & 0x3f) | 0x80);
                        d[3] = (Byte)((x & 0x3f) | 0x80);
                        d += 4;
                    }
                    else
                    {
                        // A lone surrogate, write the replacement char U+FFFD
                        d[0] = 0xef;
                        d[1] = 0xbf;
                        d[2] = 0xbd;
                        d += 3;
                    }
                    // Special chars often come in runs (non latin text), stay in the scalar path until a char that can be copied
                    if ((src < end) && IsSpecial(*src, ref esc))
                        goto Special;
                    // Text with many special chars (escapes, non ascii) is faster with the scalar path, copy up to 16 chars before trying the vectorized path again
                    var lim = (end - src) > 16 ? src + 16 : end;
                    while (src < lim)
                    {
                        x = *src;
                        if (IsSpecial(x, ref esc))
                            goto Special;
                        *d = (Byte)x;
                        ++d;
                        ++src;
                    }
                }
            }
            *d = (Byte)'"';
            w.Offset = (int)(d + 1 - org);
        }

        #endregion//Strings

    }
}
