using System;
using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace SysWeaver
{

    /// <summary>
    /// The level of detail (the smallest unit) to include when formatting a DateTime using <see cref="ValueFormat.ToValueString(DateTime, DateTimeLevels)"/>
    /// </summary>
    public enum DateTimeLevels
    {
        /// <summary>
        /// Microseconds, ex: "2024-03-05 09:07:03,012045 µs"
        /// </summary>
        Us,
        /// <summary>
        /// Milliseconds, ex: "2024-03-05 09:07:03,012 ms"
        /// </summary>
        Ms,
        /// <summary>
        /// Seconds, ex: "2024-03-05 09:07:03"
        /// </summary>
        Seconds,
        /// <summary>
        /// Minutes, ex: "2024-03-05 09:07"
        /// </summary>
        Minute,
        /// <summary>
        /// Hours, ex: "2024-03-05 09h"
        /// </summary>
        Hour,
        /// <summary>
        /// Days, ex: "2024-03-05"
        /// </summary>
        Day,
        /// <summary>
        /// Months, ex: "2024-03"
        /// </summary>
        Month,
        /// <summary>
        /// Years, ex: "2024"
        /// </summary>
        Year,
        /// <summary>
        /// The default level (Seconds)
        /// </summary>
        Default = Seconds,
    }

    /// <summary>
    /// Fast and advanced value formatting
    /// </summary>
    public static class ValueFormat
    {
        /// <summary>
        /// The length of the formatted DateTime string for every DateTimeLevels value
        /// </summary>
        static ReadOnlySpan<byte> DateTimeLengths => [
            29, // "yyyy-MM-dd HH:mm:ss,ffffff µs"
            26, // "yyyy-MM-dd HH:mm:ss,fff ms"
            19, // "yyyy-MM-dd HH:mm:ss"
            16, // "yyyy-MM-dd HH:mm"
            14, // "yyyy-MM-dd HHh"
            10, // "yyyy-MM-dd"
            7,  // "yyyy-MM"
            4,  // "yyyy"
            ];

        /// <summary>
        /// Create a string representation of a time stamp using a fixed (culture invariant, ISO 8601 like) format, using a single allocation.
        /// The formats are (depending on the level):
        /// "yyyy-MM-dd HH:mm:ss,ffffff µs", "yyyy-MM-dd HH:mm:ss,fff ms", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd HHh", "yyyy-MM-dd", "yyyy-MM" and "yyyy".
        /// </summary>
        /// <param name="value">The value</param>
        /// <param name="level">The level to show (the smallest unit that is included), smaller units are truncated (not rounded)</param>
        /// <returns>A string representation of the time stamp, ex: "2024-03-05 09:07:03" (the kind of the time stamp isn't included)</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="level"/> is not a valid DateTimeLevels value</exception>
        /// <remarks>The output is always formatted using the gregorian calendar, ':' as the time separator and ',' as the decimal separator (regardless of the current culture)</remarks>
        public static String ToValueString(this DateTime value, DateTimeLevels level = DateTimeLevels.Default)
        {
            var l = (uint)level;
            if (l > (uint)DateTimeLevels.Year)
                throw new ArgumentOutOfRangeException(nameof(level), level, "Invalid level!");
            return String.Create(DateTimeLengths[(int)l], new DateTimeState(value, level), DateTimeWriteAction);
        }

        readonly struct DateTimeState
        {
            public DateTimeState(DateTime value, DateTimeLevels level)
            {
                Value = value;
                Level = level;
            }
            public readonly DateTime Value;
            public readonly DateTimeLevels Level;
        }

        static readonly SpanAction<char, DateTimeState> DateTimeWriteAction = WriteDateTime;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Write2(Span<char> to, int offset, int value)
        {
            var t = (uint)value / 10;
            to[offset] = (char)('0' + t);
            to[offset + 1] = (char)('0' + (uint)value - t * 10);
        }

        static void WriteDigits(Span<char> to, int offset, uint value, int count)
        {
            while (count > 0)
            {
                --count;
                var t = value / 10;
                to[offset + count] = (char)('0' + value - t * 10);
                value = t;
            }
        }

        static void WriteDateTime(Span<char> to, DateTimeState state)
        {
            var t = state.Value;
            var level = state.Level;
            t.Deconstruct(out int year, out int month, out int day);
            WriteDigits(to, 0, (uint)year, 4);
            if (level >= DateTimeLevels.Year)
                return;
            to[4] = '-';
            Write2(to, 5, month);
            if (level >= DateTimeLevels.Month)
                return;
            to[7] = '-';
            Write2(to, 8, day);
            if (level >= DateTimeLevels.Day)
                return;
            var tod = t.Ticks % TimeSpan.TicksPerDay;
            var sec = (int)(tod / TimeSpan.TicksPerSecond);
            var hour = sec / 3600;
            to[10] = ' ';
            Write2(to, 11, hour);
            if (level >= DateTimeLevels.Hour)
            {
                to[13] = 'h';
                return;
            }
            sec -= hour * 3600;
            var minute = sec / 60;
            to[13] = ':';
            Write2(to, 14, minute);
            if (level >= DateTimeLevels.Minute)
                return;
            to[16] = ':';
            Write2(to, 17, sec - minute * 60);
            if (level >= DateTimeLevels.Seconds)
                return;
            to[19] = ',';
            var frac = (uint)(tod % TimeSpan.TicksPerSecond);
            if (level == DateTimeLevels.Ms)
            {
                WriteDigits(to, 20, frac / 10000, 3);
                " ms".CopyTo(to.Slice(23));
                return;
            }
            WriteDigits(to, 20, frac / 10, 6);
            " µs".CopyTo(to.Slice(26));
        }

        /// <summary>
        /// Create a string with thousands separator, optional prefix, optional suffix and optional left padding all using a single allocation
        /// </summary>
        /// <param name="value">The value</param>
        /// <param name="prefix">Optional prefix, added before the value string</param>
        /// <param name="suffix">Optional suffix, added after the value string</param>
        /// <param name="minPadLeft">Pad the string (to the left) to this minimum length (the total length, including the prefix and suffix), zero or negative for no padding</param>
        /// <param name="thousandSeparator">The thousand separator char to use</param>
        /// <param name="padChar">The padding char to use</param>
        /// <returns>A string of the format: OptionalPad + Prefix + ValueStr + Suffix (ValueStr includes a '-' sign for negative values)</returns>
        public static String ToValueString(this long value, String prefix = null, String suffix = null, int minPadLeft = 0, char thousandSeparator = ' ', char padChar = ' ')
        {
            var isNeg = value < 0;
            return InternalToValueString(isNeg ? unchecked((ulong)-value) : (ulong)value, prefix, suffix, minPadLeft, thousandSeparator, padChar, isNeg);
        }

        /// <summary>
        /// Create a string with thousands separator, optional prefix, optional suffix and optional left padding all using a single allocation
        /// </summary>
        /// <param name="value">The value</param>
        /// <param name="prefix">Optional prefix, added before the value string</param>
        /// <param name="suffix">Optional suffix, added after the value string</param>
        /// <param name="minPadLeft">Pad the string (to the left) to this minimum length (the total length, including the prefix and suffix), zero or negative for no padding</param>
        /// <param name="thousandSeparator">The thousand separator char to use</param>
        /// <param name="padChar">The padding char to use</param>
        /// <returns>A string of the format: OptionalPad + Prefix + ValueStr + Suffix</returns>
        public static String ToValueString(this ulong value, String prefix = null, String suffix = null, int minPadLeft = 0, char thousandSeparator = ' ', char padChar = ' ')
            => InternalToValueString(value, prefix, suffix, minPadLeft, thousandSeparator, padChar, false);


        /// <summary>
        /// Create a string with thousands separator, optional prefix, optional suffix and optional left padding all using a single allocation
        /// </summary>
        /// <param name="value">The value</param>
        /// <param name="prefix">Optional prefix, added before the value string</param>
        /// <param name="suffix">Optional suffix, added after the value string</param>
        /// <param name="minPadLeft">Pad the string (to the left) to this minimum length (the total length, including the prefix and suffix), zero or negative for no padding</param>
        /// <param name="thousandSeparator">The thousand separator char to use</param>
        /// <param name="padChar">The padding char to use</param>
        /// <returns>A string of the format: OptionalPad + Prefix + ValueStr + Suffix (ValueStr includes a '-' sign for negative values)</returns>
        public static String ToValueString(this int value, String prefix = null, String suffix = null, int minPadLeft = 0, char thousandSeparator = ' ', char padChar = ' ')
        {
            var isNeg = value < 0;
            return InternalToValueString(isNeg ? unchecked((uint)-value) : (uint)value, prefix, suffix, minPadLeft, thousandSeparator, padChar, isNeg);
        }


        /// <summary>
        /// Create a string with thousands separator, optional prefix, optional suffix and optional left padding all using a single allocation
        /// </summary>
        /// <param name="value">The value</param>
        /// <param name="prefix">Optional prefix, added before the value string</param>
        /// <param name="suffix">Optional suffix, added after the value string</param>
        /// <param name="minPadLeft">Pad the string (to the left) to this minimum length (the total length, including the prefix and suffix), zero or negative for no padding</param>
        /// <param name="thousandSeparator">The thousand separator char to use</param>
        /// <param name="padChar">The padding char to use</param>
        /// <returns>A string of the format: OptionalPad + Prefix + ValueStr + Suffix</returns>
        public static String ToValueString(this uint value, String prefix = null, String suffix = null, int minPadLeft = 0, char thousandSeparator = ' ', char padChar = ' ')
            => InternalToValueString(value, prefix, suffix, minPadLeft, thousandSeparator, padChar, false);

        /// <summary>
        /// Create a string with thousands separator, optional prefix, optional suffix and optional left padding all using a single allocation.
        /// The value is converted to a Decimal (15 significant digits) before it's formatted.
        /// </summary>
        /// <param name="value">The value</param>
        /// <param name="decimalCount">Number of decimals, the value is rounded to this number of decimals using banker's rounding (to even), negative values are treated as zero</param>
        /// <param name="prefix">Optional prefix, added before the value string</param>
        /// <param name="suffix">Optional suffix, added after the value string</param>
        /// <param name="minPadLeft">Pad the string (to the left) to this minimum length (the total length, including the prefix and suffix), zero or negative for no padding</param>
        /// <param name="thousandSeparator">The thousand separator char to use</param>
        /// <param name="padChar">The padding char to use</param>
        /// <param name="decimalChar">The char to use as a decimal separator</param>
        /// <returns>A string of the format: OptionalPad + Prefix + ValueStr + Suffix (ValueStr includes a '-' sign for negative values)</returns>
        /// <exception cref="OverflowException">The value is NaN, infinite or outside of the range of a Decimal</exception>
        public static String ToValueString(this Double value, int decimalCount = 2, String prefix = null, String suffix = null, int minPadLeft = 0, char thousandSeparator = ' ', char padChar = ' ', char decimalChar = '.')
            => ToValueString((Decimal)value, decimalCount, prefix, suffix, minPadLeft, thousandSeparator, padChar, decimalChar);



        /// <summary>
        /// Create a string with thousands separator, optional prefix, optional suffix and optional left padding all using a single allocation.
        /// The value is converted to a Decimal (7 significant digits) before it's formatted.
        /// </summary>
        /// <param name="value">The value</param>
        /// <param name="decimalCount">Number of decimals, the value is rounded to this number of decimals using banker's rounding (to even), negative values are treated as zero</param>
        /// <param name="prefix">Optional prefix, added before the value string</param>
        /// <param name="suffix">Optional suffix, added after the value string</param>
        /// <param name="minPadLeft">Pad the string (to the left) to this minimum length (the total length, including the prefix and suffix), zero or negative for no padding</param>
        /// <param name="thousandSeparator">The thousand separator char to use</param>
        /// <param name="padChar">The padding char to use</param>
        /// <param name="decimalChar">The char to use as a decimal separator</param>
        /// <returns>A string of the format: OptionalPad + Prefix + ValueStr + Suffix (ValueStr includes a '-' sign for negative values)</returns>
        /// <exception cref="OverflowException">The value is NaN, infinite or outside of the range of a Decimal</exception>
        public static String ToValueString(this Single value, int decimalCount = 2, String prefix = null, String suffix = null, int minPadLeft = 0, char thousandSeparator = ' ', char padChar = ' ', char decimalChar = '.')
            => ToValueString((Decimal)value, decimalCount, prefix, suffix, minPadLeft, thousandSeparator, padChar, decimalChar);


        /// <summary>
        /// Create a string with thousands separator, optional prefix, optional suffix and optional left padding all using a single allocation
        /// </summary>
        /// <param name="value">The value</param>
        /// <param name="decimalCount">Number of decimals, the value is rounded to this number of decimals using banker's rounding (to even), negative values are treated as zero</param>
        /// <param name="prefix">Optional prefix, added before the value string</param>
        /// <param name="suffix">Optional suffix, added after the value string</param>
        /// <param name="minPadLeft">Pad the string (to the left) to this minimum length (the total length, including the prefix and suffix), zero or negative for no padding</param>
        /// <param name="thousandSeparator">The thousand separator char to use</param>
        /// <param name="padChar">The padding char to use</param>
        /// <param name="decimalChar">The char to use as a decimal separator</param>
        /// <returns>A string of the format: OptionalPad + Prefix + ValueStr + Suffix (ValueStr includes a '-' sign for negative values, also if the rounded value is zero)</returns>
        [SkipLocalsInit]
        public static unsafe String ToValueString(this Decimal value, int decimalCount = 2, String prefix = null, String suffix = null, int minPadLeft = 0, char thousandSeparator = ' ', char padChar = ' ', char decimalChar = '.')
        {
            var isNeg = value < 0;
            if (isNeg)
                value = -value;
            if (decimalCount < 0)
                decimalCount = 0;
            // Banker's rounding, done without scaling the value (can't overflow)
            value = Math.Round(value, Math.Min(decimalCount, 28));
            // Max is 29 digits, a decimal point, a leading zero and a sign
            const int MaxLen = 64;
            char* digits = stackalloc char[MaxLen];
            value.TryFormat(new Span<char>(digits, MaxLen), out var len, default, CultureInfo.InvariantCulture);
            // Don't output a negative zero
            if (*digits == '-')
            {
                ++digits;
                --len;
            }
            var intLen = new ReadOnlySpan<char>(digits, len).IndexOf('.');
            int fracLen = 0;
            if (intLen < 0)
                intLen = len;
            else
                fracLen = len - intLen - 1;
            return Build(digits, intLen, digits + intLen + 1, fracLen, decimalCount, isNeg, prefix, suffix, minPadLeft, thousandSeparator, padChar, decimalChar);
        }

        /// <summary>
        /// Get the number of decimal digits required to represent a value
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int CountDigits(ulong value)
        {
            int c = 1;
            while (value >= 10000)
            {
                value /= 10000;
                c += 4;
            }
            if (value >= 10)
            {
                ++c;
                if (value >= 100)
                {
                    ++c;
                    if (value >= 1000)
                        ++c;
                }
            }
            return c;
        }

        /// <summary>
        /// Max number of chars to stackalloc in the integer formatters (larger strings are created using LargeToValueString)
        /// </summary>
        const int MaxStackChars = 512;

        [SkipLocalsInit]
        static String InternalToValueString(ulong value, String prefix, String suffix, int minPadLeft, char thousandSeparator, char padChar, bool isNeg)
        {
            var pl = prefix?.Length ?? 0;
            if (minPadLeft < 0)
                minPadLeft = 0;
            var prefixPad = (pl + minPadLeft + 3) & ~3;
            var sl = suffix?.Length ?? 0;
            int p = prefixPad + 32 + sl;
            if ((p > MaxStackChars) || (p < 0))
                return LargeToValueString(value, prefix, suffix, minPadLeft, thousandSeparator, padChar, isNeg);
            var start = p;
            Span<char> c = stackalloc char[p];
            while (sl > 0)
            {
                --sl;
                --p;
                c[p] = suffix[sl];
            }
            --p;
            var newValue = value / 10;
            c[p] = (Char)(value - (newValue * 10) + 48);
            while (newValue > 0)
            {
                value = newValue;
                newValue = value / 10;
                --p;
                if ((p & 3) == 0)
                {
                    c[p] = thousandSeparator;
                    --p;
                }
                c[p] = (Char)(value - (newValue * 10) + 48);
            }
            if (isNeg)
            {
                --p;
                c[p] = '-';
            }
            while (pl > 0)
            {
                --pl;
                --p;
                c[p] = prefix[pl];
            }
            if (minPadLeft > 0)
            {
                var l = start - p;
                while (l < minPadLeft)
                {
                    --p;
                    c[p] = padChar;
                    ++l;
                }
            }
            return new string(c.Slice(p));
        }

        [SkipLocalsInit]
        static String InternalToValueString(uint value, String prefix, String suffix, int minPadLeft, char thousandSeparator, char padChar, bool isNeg)
        {
            var pl = prefix?.Length ?? 0;
            if (minPadLeft < 0)
                minPadLeft = 0;
            var prefixPad = (pl + minPadLeft + 3) & ~3;
            var sl = suffix?.Length ?? 0;
            int p = prefixPad + 32 + sl;
            if ((p > MaxStackChars) || (p < 0))
                return LargeToValueString(value, prefix, suffix, minPadLeft, thousandSeparator, padChar, isNeg);
            var start = p;
            Span<char> c = stackalloc char[p];
            while (sl > 0)
            {
                --sl;
                --p;
                c[p] = suffix[sl];
            }
            --p;
            var newValue = value / 10;
            c[p] = (Char)(value - (newValue * 10) + 48);
            while (newValue > 0)
            {
                value = newValue;
                newValue = value / 10;
                --p;
                if ((p & 3) == 0)
                {
                    c[p] = thousandSeparator;
                    --p;
                }
                c[p] = (Char)(value - (newValue * 10) + 48);
            }
            if (isNeg)
            {
                --p;
                c[p] = '-';
            }
            while (pl > 0)
            {
                --pl;
                --p;
                c[p] = prefix[pl];
            }
            if (minPadLeft > 0)
            {
                var l = start - p;
                while (l < minPadLeft)
                {
                    --p;
                    c[p] = padChar;
                    ++l;
                }
            }
            return new string(c.Slice(p));
        }

        /// <summary>
        /// Used for strings that are too large to be built on the stack (the exact length is computed, so that only a single allocation is needed)
        /// </summary>
        static unsafe String LargeToValueString(ulong value, String prefix, String suffix, int minPadLeft, char thousandSeparator, char padChar, bool isNeg)
        {
            var intLen = CountDigits(value);
            var len = checked((prefix?.Length ?? 0) + (suffix?.Length ?? 0) + intLen + ((intLen - 1) / 3) + (isNeg ? 1 : 0));
            var padLen = minPadLeft > len ? minPadLeft - len : 0;
            BuildState s;
            s.IntDigits = null;
            s.FracDigits = null;
            s.IntValue = value;
            s.Prefix = prefix;
            s.Suffix = suffix;
            s.IntLen = intLen;
            s.FracLen = 0;
            s.DecimalCount = 0;
            s.PadLen = padLen;
            s.IsNeg = isNeg;
            s.ThousandSeparator = thousandSeparator;
            s.PadChar = padChar;
            s.DecimalChar = '.';
            return String.Create(len + padLen, s, BuildAction);
        }

        unsafe struct BuildState
        {
            public char* IntDigits;
            public char* FracDigits;
            /// <summary>
            /// Used if IntDigits is null
            /// </summary>
            public ulong IntValue;
            public String Prefix;
            public String Suffix;
            public int IntLen;
            public int FracLen;
            public int DecimalCount;
            public int PadLen;
            public bool IsNeg;
            public char ThousandSeparator;
            public char PadChar;
            public char DecimalChar;
        }

        /// <summary>
        /// Build the final string (computes the exact length, so that only a single allocation is needed)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static unsafe String Build(char* intDigits, int intLen, char* fracDigits, int fracLen, int decimalCount, bool isNeg, String prefix, String suffix, int minPadLeft, char thousandSeparator, char padChar, char decimalChar)
        {
            if (fracLen > decimalCount)
                fracLen = decimalCount;
            var len = checked((prefix?.Length ?? 0) + (suffix?.Length ?? 0) + intLen + ((intLen - 1) / 3) + (isNeg ? 1 : 0) + (decimalCount > 0 ? decimalCount + 1 : 0));
            var padLen = minPadLeft > len ? minPadLeft - len : 0;
            BuildState s;
            s.IntDigits = intDigits;
            s.FracDigits = fracDigits;
            s.IntValue = 0;
            s.Prefix = prefix;
            s.Suffix = suffix;
            s.IntLen = intLen;
            s.FracLen = fracLen;
            s.DecimalCount = decimalCount;
            s.PadLen = padLen;
            s.IsNeg = isNeg;
            s.ThousandSeparator = thousandSeparator;
            s.PadChar = padChar;
            s.DecimalChar = decimalChar;
            return String.Create(len + padLen, s, BuildAction);
        }

        static readonly SpanAction<char, BuildState> BuildAction = WriteBuild;

        static unsafe void WriteBuild(Span<char> to, BuildState s)
        {
            int o = s.PadLen;
            to.Slice(0, o).Fill(s.PadChar);
            var t = s.Prefix;
            if (t != null)
            {
                t.CopyTo(to.Slice(o));
                o += t.Length;
            }
            if (s.IsNeg)
            {
                to[o] = '-';
                ++o;
            }
            // Integer part, with thousand separators
            var d = s.IntDigits;
            var il = s.IntLen;
            var sep = s.ThousandSeparator;
            if (d == null)
            {
                // Write the digits backwards
                var value = s.IntValue;
                var e = o + il + (il - 1) / 3;
                fixed (char* dst = to)
                {
                    var p = dst + e;
                    int gc = 3;
                    for (; ; )
                    {
                        --p;
                        var n = value / 10;
                        *p = (char)('0' + (value - n * 10));
                        value = n;
                        if (value == 0)
                            break;
                        if (--gc == 0)
                        {
                            --p;
                            *p = sep;
                            gc = 3;
                        }
                    }
                }
                o = e;
                t = s.Suffix;
                if (t != null)
                    t.CopyTo(to.Slice(o));
                return;
            }
            var g = il % 3;
            if (g == 0)
                g = 3;
            for (int i = 0; ;)
            {
                new ReadOnlySpan<char>(d + i, g).CopyTo(to.Slice(o));
                o += g;
                i += g;
                if (i >= il)
                    break;
                to[o] = sep;
                ++o;
                g = 3;
            }
            // Decimals
            var dc = s.DecimalCount;
            if (dc > 0)
            {
                to[o] = s.DecimalChar;
                ++o;
                var fl = s.FracLen;
                new ReadOnlySpan<char>(s.FracDigits, fl).CopyTo(to.Slice(o));
                o += fl;
                fl = dc - fl;
                to.Slice(o, fl).Fill('0');
                o += fl;
            }
            t = s.Suffix;
            if (t != null)
                t.CopyTo(to.Slice(o));
        }

    }



}
