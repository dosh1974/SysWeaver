using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SysWeaver
{
    public static class StringExt
    {

        /// <summary>
        /// Make an culture invariant upper case version of a string
        /// </summary>
        public static readonly Func<String, String> FastLower = CultureInfo.InvariantCulture.TextInfo.ToLower;

        /// <summary>
        /// Make an culture invariant upper case version of a string
        /// </summary>
        public static readonly Func<String, String> FastUpper = CultureInfo.InvariantCulture.TextInfo.ToUpper;

        /// <summary>
        /// Make an culture invariant lower case version of a string
        /// </summary>
        /// <param name="str">The string to transform into a culture invariant lower case</param>
        /// <returns>Culture invariant lower case string</returns>
        public static String FastToLower(this String str) => str == null ? null : FastLower(str);

        /// <summary>
        /// Make an culture invariant upper case version of a string
        /// </summary>
        /// <param name="str">The string to transform into a culture invariant upper case</param>
        /// <returns>Culture invariant upper case string</returns>
        public static String FastToUpper(this String str) => str == null ? null : FastUpper(str);

        /// <summary>
        /// A fast case sensitive, invariant culture starts with method
        /// </summary>
        /// <param name="str"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool FastStartsWith(this String str, String value)
        {
            if (value == null)
                return str == null;
            if (str == null)
                return false;
            var vl = value.Length;
            if (vl == 0)
                return true;
            if (str.Length < vl)
                return false;
            return str.AsSpan(0, vl).SequenceEqual(value.AsSpan());
        }

        /// <summary>
        /// A fast case sensitive, invariant culture starts with method
        /// </summary>
        /// <param name="str"></param>
        /// <param name="value"></param>
        /// <param name="atOffset"></param>
        /// <returns></returns>
        public static bool FastStartsWith(this String str, String value, int atOffset)
        {
            if (value == null)
                return str == null;
            if (str == null)
                return false;
            var vl = value.Length;
            if (vl == 0)
                return true;
            if ((str.Length - atOffset) < vl)
                return false;
            return str.AsSpan(atOffset, vl).SequenceEqual(value.AsSpan());
        }

        /// <summary>
        /// A fast case sensitive, invariant culture ends with method
        /// </summary>
        /// <param name="str"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool FastEndsWith(this String str, String value)
        {
            if (value == null)
                return str == null;
            if (str == null)
                return false;
            var vl = value.Length;
            if (vl == 0)
                return true;
            var sl = str.Length;
            if (sl < vl)
                return false;
            return str.AsSpan(sl - vl, vl).SequenceEqual(value.AsSpan());
        }

        /// <summary>
        /// A fast case sensitive, invariant culture equals with method
        /// </summary>
        /// <param name="str"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool FastEquals(this String str, String value)
        {
            if (value == null)
                return str == null;
            if (str == null)
                return false;
            var vl = value.Length;
            if (str.Length != vl)
                return false;
            return str.AsSpan().SequenceEqual(value.AsSpan());
        }

  
        /// <summary>
        /// Extract keywords from a string (typically camelcased or filenames etc), ex:
        /// "HelloWorld42.txt" => "Hello", "World", "txt"
        /// "myBUNNY_isCool" => "my", "BUNNY", "is", "Cool" (if min len is 2)
        /// "MyFolder/Effects/CoolTorus.glsl" => "My", "Folder", "Effects", "Cool", "Torus", "glsl"
        /// </summary>
        /// <param name="str">The string to extract keywords from</param>
        /// <param name="minLen">The minimum length of a keyword</param>
        /// <returns>An enuerable with keywords</returns>
        public static IEnumerable<String> ExtractKeywords(this String str, int minLen = 2)
        {
            var l = str.Length;
            int start = 0;
            bool wasUpper = true;
            for (int i = 0; i < l; ++i)
            {
                var c = str[i];
                if (Char.IsLetter(c))
                {
                    bool isUpper = Char.IsUpper(c);
                    if (!isUpper)
                    {
                        wasUpper = false;
                        continue;
                    }
                    if (wasUpper)
                        continue;
                    var pl = i - start;
                    if (pl >= minLen)
                        yield return str.Substring(start, i - start);
                    wasUpper = isUpper;
                    start = i;
                    continue;
                }
                else
                {
                    if (i == start)
                    {
                        start = i + 1;
                        continue;
                    }
                    var pl = i - start;
                    if (pl >= minLen)
                        yield return str.Substring(start, i - start);
                    wasUpper = true;
                    start = i + 1;
                }
            }
            var ll = l - start;
            if (ll >= minLen)
                yield return str.Substring(start);
        }


        /// <summary>
        /// Extract words and numbers, ex:
        /// "'Hello world' what's up in 1974?" => "Hello", "world", "what", "s", "up", "in", "1974"
        /// "The constant PI is approximated with 3.14, or?" => "The", "constant", "PI", "is", "approximated", "with", "3.14", "or"
        /// "An invalid number such as 12.22.21 should be separated" => "An", "invalid", "number", "such", "as", "12.22", "21", "should", "be", "separated"
        /// "The depth was 32.14." => "The", "depth", "was", "32.14"
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static IEnumerable<String> ExtractWordsAndNumbers(this String str)
        {
            if (str != null)
            {
                var l = str.Length;
                int start = 0;
                bool wasDigit = false;
                bool wasOk = true;
                int dc = 0;
                for (int i = 0; i < l; ++i)
                {
                    var c = str[i];
                    var next = (i + 1) < l ? str[i + 1] : (Char)0;
                    bool isDigit = Char.IsDigit(c);
                    bool isOk = isDigit || Char.IsLetter(c);
                    if (!isOk)
                        if (wasDigit)
                            if (c == '.')
                                if (Char.IsDigit(next))
                                {
                                    isOk = dc == 0;
                                    ++dc;
                                }
                    if (isOk)
                    {
                        wasDigit = isDigit;
                        if (wasOk)
                            continue;
                        dc = 0;
                        start = i;
                        wasOk = true;
                        continue;
                    }
                    if (i == start)
                    {
                        start = i + 1;
                        continue;
                    }
                    yield return str.Substring(start, i - start);
                    start = i + 1;
                    wasDigit = false;
                    wasOk = false;
                }
                if (wasOk)
                {
                    var ll = l - start;
                    if (ll > 0)
                        yield return str.Substring(start);
                }
            }
        }


        /// <summary>
        /// Return a null string if it's an empty string (or null)
        /// </summary>
        /// <param name="str">The string</param>
        /// <returns>null if the string is null or empty</returns>
        public static String NullIfEmpty(this String str) => String.IsNullOrEmpty(str) ? null : str;

        /// <summary>
        /// Similar to String.Join but excludes all empty texts
        /// </summary>
        /// <param name="separator"></param>
        /// <param name="texts"></param>
        /// <returns></returns>
        public static String JoinNonEmpty(String separator, params String[] texts) => texts == null ? null : String.Join(separator, texts.Where(x => !String.IsNullOrEmpty(x)));



        /// <summary>
        /// Interleaves the characters from two equally length strings.
        /// Ex: "abc", "123" => "a1b2c3".
        /// </summary>
        /// <param name="a">One string, ex: "abc"</param>
        /// <param name="b">Another string, ex: "123"</param>
        /// <returns>The interleaved result, ex: "a1b2c3"</returns>
        /// <exception cref="Exception"></exception>
        public static String Interleave(this String a, String b)
        {
            var al = a.Length;
            if (b.Length != al)
                throw new Exception("Must be the same length!");
            Span<Char> res = stackalloc Char[al + al];
            for (int i = 0, o = 0; i < al; ++ i)
            {
                res[o] = a[i];
                ++o;
                res[o] = b[i];
                ++o;
            }
            return new string(res);
        }




    }


    public static class ValueFormat
    {

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


    }



}
