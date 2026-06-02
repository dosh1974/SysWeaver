using System;

namespace SysWeaver
{

    public enum DateTimeLevels
    {
        Us,
        Ms,
        Seconds,
        Minute,
        Hour,
        Day,
        Month,
        Year,
        Default = Seconds,
    }

    /// <summary>
    /// Fast and advanced value formatting
    /// </summary>
    public static class ValueFormat
    {
        static readonly String[] DateTimeFormats = [
            "yyyy-MM-dd HH:mm:ss,ffffff", " µs",
            "yyyy-MM-dd HH:mm:ss,fff", " ms",
            "yyyy-MM-dd HH:mm:ss", "",
            "yyyy-MM-dd HH:mm", "",
            "yyyy-MM-dd HH", "h",
            "yyyy-MM-dd", "",
            "yyyy-MM", "",
            "yyyy", "",
            ];

        /// <summary>
        /// Create a string with thousands separator, optional prefix, optional suffix and optional left padding all using a with single allocation
        /// </summary>
        /// <param name="value">The value</param>
        /// <param name="level">The level to show</param>
        /// <returns>A string of the format: OptionalPad + Prefix + ValueStr + Suffix</returns>
        public static String ToValueString(this DateTime value, DateTimeLevels level = DateTimeLevels.Default)
        {
            int i = (int)level;
            var d = DateTimeFormats;
            i += i;
            return value.ToString(d[i]) + d[i + 1];
        }

        /// <summary>
        /// Create a string with thousands separator, optional prefix, optional suffix and optional left padding all using a with single allocation
        /// </summary>
        /// <param name="value">The value</param>
        /// <param name="prefix">Optional prefix, added before the value string</param>
        /// <param name="suffix">Optional suffix, added after the value string</param>
        /// <param name="minPadLeft">Pad the string (to the left) to this minimum length</param>
        /// <param name="thousandSeparator">The thousand separator char to use</param>
        /// <param name="padChar">The padding char to use</param>
        /// <returns>A string of the format: OptionalPad + Prefix + ValueStr + Suffix</returns>
        public static String ToValueString(this long value, String prefix = null, String suffix = null, int minPadLeft = 0, char thousandSeparator = ' ', char padChar = ' ')
        {
            var isNeg = value < 0;
            if (isNeg)
                value = -value;
            return InternalToValueString((ulong)value, prefix, suffix, minPadLeft, thousandSeparator, padChar, isNeg);
        }

        /// <summary>
        /// Create a string with thousands separator, optional prefix, optional suffix and optional left padding all using a with single allocation
        /// </summary>
        /// <param name="value">The value</param>
        /// <param name="prefix">Optional prefix, added before the value string</param>
        /// <param name="suffix">Optional suffix, added after the value string</param>
        /// <param name="minPadLeft">Pad the string (to the left) to this minimum length</param>
        /// <param name="thousandSeparator">The thousand separator char to use</param>
        /// <param name="padChar">The padding char to use</param>
        /// <returns>A string of the format: OptionalPad + Prefix + ValueStr + Suffix</returns>
        public static String ToValueString(this ulong value, String prefix = null, String suffix = null, int minPadLeft = 0, char thousandSeparator = ' ', char padChar = ' ')
            => InternalToValueString(value, prefix, suffix, minPadLeft, thousandSeparator, padChar, false);


        /// <summary>
        /// Create a string with thousands separator, optional prefix, optional suffix and optional left padding all using a with single allocation
        /// </summary>
        /// <param name="value">The value</param>
        /// <param name="prefix">Optional prefix, added before the value string</param>
        /// <param name="suffix">Optional suffix, added after the value string</param>
        /// <param name="minPadLeft">Pad the string (to the left) to this minimum length</param>
        /// <param name="thousandSeparator">The thousand separator char to use</param>
        /// <param name="padChar">The padding char to use</param>
        /// <returns>A string of the format: OptionalPad + Prefix + ValueStr + Suffix</returns>
        public static String ToValueString(this int value, String prefix = null, String suffix = null, int minPadLeft = 0, char thousandSeparator = ' ', char padChar = ' ')
        {
            var isNeg = value < 0;
            if (isNeg)
                value = -value;
            return InternalToValueString((uint)value, prefix, suffix, minPadLeft, thousandSeparator, padChar, isNeg);
        }


        /// <summary>
        /// Create a string with thousands separator, optional prefix, optional suffix and optional left padding all using a with single allocation
        /// </summary>
        /// <param name="value">The value</param>
        /// <param name="prefix">Optional prefix, added before the value string</param>
        /// <param name="suffix">Optional suffix, added after the value string</param>
        /// <param name="minPadLeft">Pad the string (to the left) to this minimum length</param>
        /// <param name="thousandSeparator">The thousand separator char to use</param>
        /// <param name="padChar">The padding char to use</param>
        /// <returns>A string of the format: OptionalPad + Prefix + ValueStr + Suffix</returns>
        public static String ToValueString(this uint value, String prefix = null, String suffix = null, int minPadLeft = 0, char thousandSeparator = ' ', char padChar = ' ')
            => InternalToValueString(value, prefix, suffix, minPadLeft, thousandSeparator, padChar, false);

        /// <summary>
        /// Create a string with thousands separator, optional prefix, optional suffix and optional left padding all using a with single allocation
        /// </summary>
        /// <param name="value">The value</param>
        /// <param name="decimalCount">Numer of decimals</param>
        /// <param name="prefix">Optional prefix, added before the value string</param>
        /// <param name="suffix">Optional suffix, added after the value string</param>
        /// <param name="minPadLeft">Pad the string (to the left) to this minimum length</param>
        /// <param name="thousandSeparator">The thousand separator char to use</param>
        /// <param name="padChar">The padding char to use</param>
        /// <param name="decimalChar">The char to use as a decimal sparator</param>
        /// <returns>A string of the format: OptionalPad + Prefix + ValueStr + Suffix</returns>
        public static String ToValueString(this Double value, int decimalCount = 2, String prefix = null, String suffix = null, int minPadLeft = 0, char thousandSeparator = ' ', char padChar = ' ', char decimalChar = '.')
            => ToValueString((Decimal)value, decimalCount, prefix, suffix, minPadLeft, thousandSeparator, padChar, decimalChar);

        /// <summary>
        /// Create a string with thousands separator, optional prefix, optional suffix and optional left padding all using a with single allocation
        /// </summary>
        /// <param name="value">The value</param>
        /// <param name="decimalCount">Numer of decimals</param>
        /// <param name="prefix">Optional prefix, added before the value string</param>
        /// <param name="suffix">Optional suffix, added after the value string</param>
        /// <param name="minPadLeft">Pad the string (to the left) to this minimum length</param>
        /// <param name="thousandSeparator">The thousand separator char to use</param>
        /// <param name="padChar">The padding char to use</param>
        /// <param name="decimalChar">The char to use as a decimal sparator</param>
        /// <returns>A string of the format: OptionalPad + Prefix + ValueStr + Suffix</returns>
        public static String ToValueString(this Decimal value, int decimalCount = 2, String prefix = null, String suffix = null, int minPadLeft = 0, char thousandSeparator = ' ', char padChar = ' ', char decimalChar = '.')
        {
            var isNeg = value < 0;
            if (isNeg)
                value = -value;
            var pl = prefix?.Length ?? 0;
            var prefixPad = (pl + minPadLeft + 3) & ~3;
            var sl = suffix?.Length ?? 0;
            int p = prefixPad + 32 + sl;
            if (decimalCount > 0)
                p += (decimalCount + 1);
            var start = p;
            Span<char> c = stackalloc char[p];
            while (sl > 0)
            {
                --sl;
                --p;
                c[p] = suffix[sl];
            }
            if (decimalCount > 0)
            {
                for (int i = 0; i < decimalCount; ++i)
                    value *= 10;
                value = Math.Round(value);
                while (decimalCount > 0)
                {
                    --decimalCount;
                    var dnewValue = Math.Truncate(value / 10);
                    --p;
                    c[p] = (Char)(value - (dnewValue * 10) + 48);
                    value = dnewValue;
                }
                --p;
                c[p] = '.';
            }
            var newValue = Math.Truncate(value / 10);
            --p;
            c[p] = (Char)(value - (newValue * 10) + 48);
            while (newValue > 0)
            {
                value = newValue;
                newValue = Math.Truncate(value / 10);
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


        static String InternalToValueString(ulong value, String prefix, String suffix, int minPadLeft, char thousandSeparator, char padChar, bool isNeg)
        {
            var pl = prefix?.Length ?? 0;
            var prefixPad = (pl + minPadLeft + 3) & ~3;
            var sl = suffix?.Length ?? 0;
            int p = prefixPad + 32 + sl;
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


        static String InternalToValueString(uint value, String prefix, String suffix, int minPadLeft, char thousandSeparator, char padChar, bool isNeg)
        {
            var pl = prefix?.Length ?? 0;
            var prefixPad = (pl + minPadLeft + 3) & ~3;
            var sl = suffix?.Length ?? 0;
            int p = prefixPad + 32 + sl;
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

    }



}
