using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;

namespace SysWeaver
{
    public static class StringExt
    {

        static readonly TextInfo Ti = CultureInfo.InvariantCulture.TextInfo;
        static readonly CompareInfo Ci = CultureInfo.InvariantCulture.CompareInfo;

        #region FastToLower


        /// <summary>
        /// Returns true if a string is all lowercased (invariant)
        /// </summary>
        /// <param name="str">The string to test</param>
        /// <returns>True if all chars are lowercased (or not chars at all)</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool FastIsLower(this String str)
            => InternalFastIsLower(str.AsSpan(), str.Length);

        /// <summary>
        /// Returns true if a string is all lowercased (invariant)
        /// </summary>
        /// <param name="str">The string to test</param>
        /// <param name="startIndex">The index of the first character to test</param>
        /// <param name="length">The number of characters to test</param>
        /// <returns>True if all chars are lowercased (or not chars at all)</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool FastIsLower(this String str, int startIndex, int length = -1)
        {
            var s = str.AsSpan(startIndex);
            return InternalFastIsLower(s, length < 0 ? s.Length : length);

        }

        /// <summary>
        /// Returns true if all chars are lowercased (invariant)
        /// </summary>
        /// <param name="source">The chars</param>
        /// <param name="l">The chars</param>
        /// <returns>True if all chars are lowercased (or not chars at all)</returns>
        static bool InternalFastIsLower(this ReadOnlySpan<Char> source, int l)
        {
            while (l > 0)
            {
                --l;
                var c = source[l];
                if (c != Char.ToLowerInvariant(c))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Make a culture invariant lower case version of a substring
        /// </summary>
        /// <param name="str">The string to transform into a culture invariant lower case</param>
        /// <param name="startIndex">The index of the first character to convert</param>
        /// <param name="length">The number of characters to convert</param>
        /// <returns>Culture invariant lower case string</returns>
        public static String FastToLower(this String str, int startIndex, int length = -1)
        {
            var s = str.AsSpan(startIndex);
            var sl = s.Length;
            if (length < 0)
                length = sl;
            if ((startIndex == 0) && (length == sl))
                return Ti.ToLower(str);
            var isLower = InternalFastIsLower(s, length);
            return isLower ? new string(s[..length]) : String.Create(length, s, LowerCasedSubString);
        }


        /// <summary>
        /// Make a culture invariant lower case version of the first N chars of a string
        /// </summary>
        /// <param name="str">The string to transform into a culture invariant lower case</param>
        /// <param name="length">The number of characters to convert</param>
        /// <returns>Culture invariant lower case string</returns>
        public static String FastStartToLower(this String str, int length)
        {
            var s = str.AsSpan();
            if (length == str.Length)
                return Ti.ToLower(str);
            var isLower = InternalFastIsLower(s, length);
            return isLower ? new string(s[..length]) : String.Create(length, s, LowerCasedSubString);
        }


        static readonly SpanAction<Char, ReadOnlySpan<Char>> LowerCasedSubString = (str, source) =>
        {
            var l = str.Length;
            int i = 0;

            var p = str;
            var l4 = l & ~3;
            while (i < l4)
            {
                var a = source[i];
                ++i;
                var b = source[i];
                ++i;
                var c = source[i];
                ++i;
                var d = source[i];
                ++i;
                p[0] = Char.ToLowerInvariant(a);
                p[1] = Char.ToLowerInvariant(b);
                p[2] = Char.ToLowerInvariant(c);
                p[3] = Char.ToLowerInvariant(d);
                p = p[4..];
            }
            while (i < l)
            {
                str[i] = Char.ToLowerInvariant(source[i]);
                ++i;
            }
        };

        /// <summary>
        /// Make a trimmed culture invariant lower case version of a string
        /// </summary>
        /// <param name="str">The string to transform into a trimmed culture invariant lower case</param>
        /// <returns>A trimmed culture invariant lower cased string</returns>
        public static String FastTrimToLower(this String str)
        {
            var s = str.AsSpan();
            var sl = s.Length;
            int ss = 0;
            int ee = sl;
            while ((ss < ee) && Char.IsWhiteSpace(s[ss]))
                ++ ss;
            if (ss >= ee)
                return String.Empty;
            while ((ee > ss) && Char.IsWhiteSpace(s[ee - 1]))
                -- ee;
            if ((ss == 0) && (ee == sl))
                return Ti.ToLower(str);
            s = s[ss..];
            var l = ee - ss;
            var isLower = InternalFastIsLower(s, l);
            return isLower ? new String(s[..l]) : String.Create(l, s, LowerCasedSubString);
        }

        /// <summary>
        /// Make a trimmed (at start) culture invariant lower case version of a string
        /// </summary>
        /// <param name="str">The string to transform into a trimmed (at start) culture invariant lower case</param>
        /// <returns>A trimmed (at start) culture invariant lower cased string</returns>
        public static String FastTrimStartToLower(this String str)
        {
            var s = str.AsSpan();
            int ss = 0;
            int ee = s.Length;
            while ((ss < ee) && Char.IsWhiteSpace(s[ss]))
                ++ss;
            if (ss >= ee)
                return String.Empty;
            if (ss == 0)
                return Ti.ToLower(str);
            s = s[ss..];
            var l = ee - ss;
            var isLower = InternalFastIsLower(s, l);
            return isLower ? new String(s) : String.Create(l, s, LowerCasedSubString);
        }

        /// <summary>
        /// Make a trimmed (at end) culture invariant lower case version of a string
        /// </summary>
        /// <param name="str">The string to transform into a trimmed (at end) culture invariant lower case</param>
        /// <returns>A trimmed (at end) culture invariant lower cased string</returns>
        public static String FastTrimEndToLower(this String str)
        {
            var s = str.AsSpan();
            var sl = s.Length;
            var ee = sl;
            while ((ee > 0) && Char.IsWhiteSpace(s[ee - 1]))
                --ee;
            if (ee <= 0)
                return String.Empty;
            if (ee == sl)
                return Ti.ToLower(str);
            var isLower = InternalFastIsLower(s, ee);
            return isLower ? new String(s[..ee]) : String.Create(ee, s, LowerCasedSubString);
        }



        /// <summary>
        /// Make a trimmed culture invariant lower case version of a string
        /// </summary>
        /// <param name="str">The string to transform into a trimmed culture invariant lower case</param>
        /// <param name="trimChar">The character to trim</param>
        /// <returns>A trimmed culture invariant lower cased string</returns>
        public static String FastTrimToLower(this String str, Char trimChar)
        {
            var s = str.AsSpan();
            var sl = s.Length;
            int ss = 0;
            var ee = sl;
            while ((ss < ee) && (s[ss] == trimChar))
                ++ss;
            if (ss >= ee)
                return String.Empty;
            while ((ee > ss) && (s[ee - 1] == trimChar))
                --ee;
            if (ss == 0)
                return Ti.ToLower(str);
            s = s[ss..];
            var l = ee - ss;
            var isLower = InternalFastIsLower(s, l);
            return isLower ? new String(s) : String.Create(l, s, LowerCasedSubString);
        }

        /// <summary>
        /// Make a trimmed (at start) culture invariant lower case version of a string
        /// </summary>
        /// <param name="str">The string to transform into a trimmed (at start) culture invariant lower case</param>
        /// <param name="trimChar">The character to trim</param>
        /// <returns>A trimmed (at start) culture invariant lower cased string</returns>
        public static String FastTrimStartToLower(this String str, Char trimChar)
        {
            var s = str.AsSpan();
            int ss = 0;
            var ee = s.Length;
            while ((ss < ee) && (s[ss] == trimChar))
                ++ss;
            if (ss >= ee)
                return String.Empty;
            if (ss == 0)
                return Ti.ToLower(str);
            s = s[ss..];
            var l = ee - ss;
            var isLower = InternalFastIsLower(s, l);
            return isLower ? new String(s) : String.Create(l, s, LowerCasedSubString);
        }

        /// <summary>
        /// Make a trimmed (at end) culture invariant lower case version of a string
        /// </summary>
        /// <param name="str">The string to transform into a trimmed (at end) culture invariant lower case</param>
        /// <param name="trimChar">The character to trim</param>
        /// <returns>A trimmed (at end) culture invariant lower cased string</returns>
        public static String FastTrimEndToLower(this String str, Char trimChar)
        {
            var s = str.AsSpan();
            var sl = s.Length;
            var ee = sl;
            while ((ee > 0) && (s[ee - 1] == trimChar))
                --ee;
            if (ee <= 0)
                return String.Empty;
            if (ee == sl)
                return Ti.ToLower(str);
            var isLower = InternalFastIsLower(s, ee);
            return isLower ? new String(s[..ee]) : String.Create(ee, s, LowerCasedSubString);
        }

        /// <summary>
        /// Make a culture invariant lower case version of a string
        /// </summary>
        /// <param name="str">The string to transform into a culture invariant lower case</param>
        /// <returns>Culture invariant lower case string</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String FastToLower(this String str)
            => InternalFastIsLower(str.AsSpan(), str.Length) ? str : Ti.ToLower(str);

        #endregion//FastToLower


        #region FastToUpper


        /// <summary>
        /// Returns true if a string is all uppercased (invariant)
        /// </summary>
        /// <param name="str">The string to test</param>
        /// <returns>True if all chars are uppercased (or not chars at all)</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool FastIsUpper(this String str)
            => InternalFastIsUpper(str.AsSpan(), str.Length);

        /// <summary>
        /// Returns true if a string is all uppercased (invariant)
        /// </summary>
        /// <param name="str">The string to test</param>
        /// <param name="startIndex">The index of the first character to test</param>
        /// <param name="length">The number of characters to test</param>
        /// <returns>True if all chars are uppercased (or not chars at all)</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool FastIsUpper(this String str, int startIndex, int length = -1)
        {
            var s = str.AsSpan(startIndex);
            return InternalFastIsUpper(s, length < 0 ? s.Length : length);

        }

        /// <summary>
        /// Returns true if all chars are uppercased (invariant)
        /// </summary>
        /// <param name="source">The chars</param>
        /// <param name="l">The chars</param>
        /// <returns>True if all chars are uppercased (or not chars at all)</returns>
        static bool InternalFastIsUpper(this ReadOnlySpan<Char> source, int l)
        {
            while (l > 0)
            {
                --l;
                var c = source[l];
                if (c != Char.ToUpperInvariant(c))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Make a culture invariant upper case version of a substring
        /// </summary>
        /// <param name="str">The string to transform into a culture invariant upper case</param>
        /// <param name="startIndex">The index of the first character to convert</param>
        /// <param name="length">The number of characters to convert</param>
        /// <returns>Culture invariant upper case string</returns>
        public static String FastToUpper(this String str, int startIndex, int length = -1)
        {
            var s = str.AsSpan(startIndex);
            var sl = s.Length;
            if (length < 0)
                length = sl;
            if ((startIndex == 0) && (length == sl))
                return Ti.ToUpper(str);
            var isUpper = InternalFastIsUpper(s, length);
            return isUpper ? new string(s[..length]) : String.Create(length, s, UpperCasedSubString);
        }


        /// <summary>
        /// Make a culture invariant upper case version of the first N chars of a string
        /// </summary>
        /// <param name="str">The string to transform into a culture invariant upper case</param>
        /// <param name="length">The number of characters to convert</param>
        /// <returns>Culture invariant upper case string</returns>
        public static String FastStartToUpper(this String str, int length)
        {
            var s = str.AsSpan();
            if (length == str.Length)
                return Ti.ToUpper(str);
            var isUpper = InternalFastIsUpper(s, length);
            return isUpper ? new string(s[..length]) : String.Create(length, s, UpperCasedSubString);
        }


        static readonly SpanAction<Char, ReadOnlySpan<Char>> UpperCasedSubString = (str, source) =>
        {
            var l = str.Length;
            int i = 0;

            var p = str;
            var l4 = l & ~3;
            while (i < l4)
            {
                var a = source[i];
                ++i;
                var b = source[i];
                ++i;
                var c = source[i];
                ++i;
                var d = source[i];
                ++i;
                p[0] = Char.ToUpperInvariant(a);
                p[1] = Char.ToUpperInvariant(b);
                p[2] = Char.ToUpperInvariant(c);
                p[3] = Char.ToUpperInvariant(d);
                p = p[4..];
            }
            while (i < l)
            {
                str[i] = Char.ToUpperInvariant(source[i]);
                ++i;
            }
        };

        /// <summary>
        /// Make a trimmed culture invariant upper case version of a string
        /// </summary>
        /// <param name="str">The string to transform into a trimmed culture invariant upper case</param>
        /// <returns>A trimmed culture invariant upper cased string</returns>
        public static String FastTrimToUpper(this String str)
        {
            var s = str.AsSpan();
            var sl = s.Length;
            int ss = 0;
            int ee = sl;
            while ((ss < ee) && Char.IsWhiteSpace(s[ss]))
                ++ss;
            if (ss >= ee)
                return String.Empty;
            while ((ee > ss) && Char.IsWhiteSpace(s[ee - 1]))
                --ee;
            if ((ss == 0) && (ee == sl))
                return Ti.ToUpper(str);
            s = s[ss..];
            var l = ee - ss;
            var isUpper = InternalFastIsUpper(s, l);
            return isUpper ? new String(s[..l]) : String.Create(l, s, UpperCasedSubString);
        }

        /// <summary>
        /// Make a trimmed (at start) culture invariant upper case version of a string
        /// </summary>
        /// <param name="str">The string to transform into a trimmed (at start) culture invariant upper case</param>
        /// <returns>A trimmed (at start) culture invariant upper cased string</returns>
        public static String FastTrimStartToUpper(this String str)
        {
            var s = str.AsSpan();
            int ss = 0;
            int ee = s.Length;
            while ((ss < ee) && Char.IsWhiteSpace(s[ss]))
                ++ss;
            if (ss >= ee)
                return String.Empty;
            if (ss == 0)
                return Ti.ToUpper(str);
            s = s[ss..];
            var l = ee - ss;
            var isUpper = InternalFastIsUpper(s, l);
            return isUpper ? new String(s) : String.Create(l, s, UpperCasedSubString);
        }

        /// <summary>
        /// Make a trimmed (at end) culture invariant upper case version of a string
        /// </summary>
        /// <param name="str">The string to transform into a trimmed (at end) culture invariant upper case</param>
        /// <returns>A trimmed (at end) culture invariant upper cased string</returns>
        public static String FastTrimEndToUpper(this String str)
        {
            var s = str.AsSpan();
            var sl = s.Length;
            var ee = sl;
            while ((ee > 0) && Char.IsWhiteSpace(s[ee - 1]))
                --ee;
            if (ee <= 0)
                return String.Empty;
            if (ee == sl)
                return Ti.ToUpper(str);
            var isUpper = InternalFastIsUpper(s, ee);
            return isUpper ? new String(s[..ee]) : String.Create(ee, s, UpperCasedSubString);
        }



        /// <summary>
        /// Make a trimmed culture invariant upper case version of a string
        /// </summary>
        /// <param name="str">The string to transform into a trimmed culture invariant upper case</param>
        /// <param name="trimChar">The character to trim</param>
        /// <returns>A trimmed culture invariant upper cased string</returns>
        public static String FastTrimToUpper(this String str, Char trimChar)
        {
            var s = str.AsSpan();
            var sl = s.Length;
            int ss = 0;
            var ee = sl;
            while ((ss < ee) && (s[ss] == trimChar))
                ++ss;
            if (ss >= ee)
                return String.Empty;
            while ((ee > ss) && (s[ee - 1] == trimChar))
                --ee;
            if (ss == 0)
                return Ti.ToUpper(str);
            s = s[ss..];
            var l = ee - ss;
            var isUpper = InternalFastIsUpper(s, l);
            return isUpper ? new String(s) : String.Create(l, s, UpperCasedSubString);
        }

        /// <summary>
        /// Make a trimmed (at start) culture invariant upper case version of a string
        /// </summary>
        /// <param name="str">The string to transform into a trimmed (at start) culture invariant upper case</param>
        /// <param name="trimChar">The character to trim</param>
        /// <returns>A trimmed (at start) culture invariant upper cased string</returns>
        public static String FastTrimStartToUpper(this String str, Char trimChar)
        {
            var s = str.AsSpan();
            int ss = 0;
            var ee = s.Length;
            while ((ss < ee) && (s[ss] == trimChar))
                ++ss;
            if (ss >= ee)
                return String.Empty;
            if (ss == 0)
                return Ti.ToUpper(str);
            s = s[ss..];
            var l = ee - ss;
            var isUpper = InternalFastIsUpper(s, l);
            return isUpper ? new String(s) : String.Create(l, s, UpperCasedSubString);
        }

        /// <summary>
        /// Make a trimmed (at end) culture invariant upper case version of a string
        /// </summary>
        /// <param name="str">The string to transform into a trimmed (at end) culture invariant upper case</param>
        /// <param name="trimChar">The character to trim</param>
        /// <returns>A trimmed (at end) culture invariant upper cased string</returns>
        public static String FastTrimEndToUpper(this String str, Char trimChar)
        {
            var s = str.AsSpan();
            var sl = s.Length;
            var ee = sl;
            while ((ee > 0) && (s[ee - 1] == trimChar))
                --ee;
            if (ee <= 0)
                return String.Empty;
            if (ee == sl)
                return Ti.ToUpper(str);
            var isUpper = InternalFastIsUpper(s, ee);
            return isUpper ? new String(s[..ee]) : String.Create(ee, s, UpperCasedSubString);
        }

        /// <summary>
        /// Make a culture invariant upper case version of a string
        /// </summary>
        /// <param name="str">The string to transform into a culture invariant upper case</param>
        /// <returns>Culture invariant upper case string</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String FastToUpper(this String str)
            => InternalFastIsUpper(str.AsSpan(), str.Length) ? str : Ti.ToUpper(str);

        #endregion//FastToUpper

        /// <summary>
        /// A fast case sensitive, invariant culture starts with method
        /// </summary>
        /// <param name="str"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool FastStartsWith(this String str, String value)
        {
            /*if (value == null)
                return str == null;
            if (str == null)
                return false;*/
            var vl = value.Length;
            if (vl > str.Length)
                return false;
            return str.AsSpan(0, value.Length).SequenceEqual(value.AsSpan());
        }

        /// <summary>
        /// A fast case sensitive, invariant culture starts with method
        /// </summary>
        /// <param name="str"></param>
        /// <param name="value"></param>
        /// <param name="atOffset"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool FastStartsWith(this String str, String value, int atOffset)
        {
            /*if (value == null)
                return str == null;
            if (str == null)
                return false;*/
            var vl = value.Length;
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool FastEndsWith(this String str, String value)
            => str.EndsWith(value, StringComparison.Ordinal);
/*        {
            var vl = value.Length;
            var sl = str.Length;
            if (vl > sl)
                return false;
            return str.AsSpan(sl - vl, vl).SequenceEqual(value.AsSpan());
        }
*/


        /// <summary>
        /// using case sensitive, invariant culture 
        /// </summary>
        /// <param name="str"></param>
        /// <param name="value">The text to search for</param>
        /// <returns>-1 if not found or the position where the string was found</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FastIndexOf(this String str, String value)
            => str.AsSpan().IndexOf(value.AsSpan());

        /// <summary>
        /// using case sensitive, invariant culture 
        /// </summary>
        /// <param name="str"></param>
        /// <param name="value">The text to search for</param>
        /// <param name="startPos">The start position for the search</param>
        /// <returns>-1 if not found or the position where the string was found</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FastIndexOf(this String str, String value, int startPos)
        {
            var t = str.AsSpan(startPos).IndexOf(value.AsSpan());
            if (t >= 0)
                t += startPos;
            return t;
        }


        /// <summary>
        /// using case sensitive, invariant culture 
        /// </summary>
        /// <param name="str"></param>
        /// <param name="value">The text to search for</param>
        /// <returns>-1 if not found or the position where the string was found</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FastLastIndexOf(this String str, String value)
            => str.AsSpan().LastIndexOf(value.AsSpan());

        /// <summary>
        /// using case sensitive, invariant culture 
        /// </summary>
        /// <param name="str"></param>
        /// <param name="value">The text to search for</param>
        /// <param name="startPos">The start position for the search</param>
        /// <returns>-1 if not found or the position where the string was found</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FastLastIndexOf(this String str, String value, int startPos)
            => str.AsSpan(0, startPos).LastIndexOf(value.AsSpan());

        /// <summary>
        /// A fast case sensitive, invariant culture equals with method
        /// </summary>
        /// <param name="str"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool FastEquals(this String str, String value)
            => String.Equals(str, value, StringComparison.Ordinal);

//        {
/*            if (value == null)
                return str == null;
            if (str == null)
                return false;
*/ 
/*return str.AsSpan().SequenceEqual(value.AsSpan());
        }
*/

        /// <summary>
        /// A fast case sensitive, invariant culture equals with method
        /// </summary>
        /// <param name="str"></param>
        /// <param name="strStart">Start offset into the str, equal to str.SubString(strStart, strLen).FastEquals(value)</param>
        /// <param name="strLen">Length of the str, equal to str.SubString(strStart, strLen).FastEquals(value)</param>
        /// <param name="value"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool FastSubEquals(this String str, int strStart, int strLen, String value)
        {
/*            if (value == null)
                return str == null;
            if (str == null)
                return false;
*/            var len = value.Length;
            if (len != strLen)
                return false;
            var ml = strStart + len;
            if (str.Length < ml)
                return false;
            return str.AsSpan(strStart, len).SequenceEqual(value.AsSpan());
        }


        /// <summary>
        /// A fast case sensitive, invariant culture equals with method
        /// </summary>
        /// <param name="str"></param>
        /// <param name="strStart">Start offset into the str, equal to str.SubString(strStart).FastEquals(value)</param>
        /// <param name="value"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool FastSubEquals(this String str, int strStart, String value)
        {
/*            if (value == null)
                return str == null;
            if (str == null)
                return false;
*/            var len = value.Length;
            var ml = strStart + len;
            if (str.Length != ml)
                return false;
            return str.AsSpan(strStart, len).SequenceEqual(value.AsSpan());
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String NullIfEmpty(this String str) => String.IsNullOrEmpty(str) ? null : str;

        /// <summary>
        /// Similar to String.Join but excludes all empty texts
        /// </summary>
        /// <param name="separator"></param>
        /// <param name="texts"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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



        /// <summary>
        /// Check if a word is found in some text, the glyph before a word may no be a letter, the glyph after a word may not be a letter.
        /// </summary>
        /// <param name="sentence"></param>
        /// <param name="word"></param>
        /// <param name="cmp"></param>
        /// <returns></returns>
        public static bool ContainsWord(this String sentence, String word, StringComparison cmp = StringComparison.OrdinalIgnoreCase)
        {
            var sl = sentence.Length;
            var wl = word.Length;
            int s = 0;
            for (; ; )
            {
                s = sentence.IndexOf(word, s, cmp);
                if (s < 0)
                    return false;
                var o = s - 1;
                s += wl;
                if (o >= 0)
                    if (Char.IsLetter(sentence[o]))
                        continue;
                if (s < sl)
                    if (Char.IsLetter(sentence[s]))
                        continue;
                return true;
            }
        }

        /// <summary>
        /// Remove all diacritics from a string (replaces them with base values)
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static string RemoveDiacritics(this string text)
        {
            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder(capacity: normalizedString.Length);

            for (int i = 0; i < normalizedString.Length; i++)
            {
                char c = normalizedString[i];
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }
            return stringBuilder
                .ToString()
                .Normalize(NormalizationForm.FormC);
        }

        /// <summary>
        /// Remove one set of quotes from a a string (if they exist).
        /// Ex: "apa" => apa
        /// 'banana' => banana
        /// ""monkey"" => "monkey"
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static string RemoveQuotes(this string text)
        {
            if (text == null)
                return text;
            var tl = text.Length;
            if (tl < 2)
                return text;
            var c = text[0];
            if (c != '"')
                if (c != '\'')
                    return text;
            if (text[tl - 1] != c)
                return text;
            return new string(text.AsSpan(1, tl - 2));
        }


        /// <summary>
        /// Count the number of occurances of a substring
        /// </summary>
        /// <param name="text"></param>
        /// <param name="subString"></param>
        /// <param name="com"></param>
        /// <returns></returns>
        public static int Count(this String text, String subString, StringComparison com = StringComparison.CurrentCulture)
        {
            int c = 0;
            var l = subString.Length;
            for (int p = 0; ;)
            {
                p = text.IndexOf(subString, p, com);
                if (p < 0)
                    return c;
                ++c;
                p += l;
            }
        }


        /// <summary>
        /// Remove all occurances of some chars from a string.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="removeChars">The chars to remove</param>
        /// <returns></returns>
        public static String RemoveChars(this String text, params Char[] removeChars)
        {
            if (removeChars == null)
                return text;
            if (removeChars.Length <= 0)
                return text;
            var remove = new HashSet<Char>(removeChars);
            return InternalRemoveChars(text, remove);
        }

        /// <summary>
        /// Remove all occurances of some chars from a string.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="removeChars">The chars to remove</param>
        /// <returns></returns>
        public static String RemoveChars(this String text, String removeChars)
        {
            if (removeChars == null)
                return text;
            if (removeChars.Length <= 0)
                return text;
            var remove = new HashSet<Char>(removeChars);
            return InternalRemoveChars(text, remove);
        }

        /// <summary>
        /// Remove all occurances of some chars from a string.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="removeChars">The chars to remove</param>
        /// <returns></returns>
        public static String RemoveChars(this String text, IReadOnlySet<Char> removeChars)
        {
            if (removeChars == null)
                return text;
            if (removeChars.Count <= 0)
                return text;
            return InternalRemoveChars(text, removeChars);
        }

        static String InternalRemoveChars(String text, IReadOnlySet<Char> remove)
        {
            if (text == null)
                return text;
            var l = text.Length;
            if (l <= 0)
                return text;
            Char[] o = null;
            int d = 0;
            for (int i = 0; i < l; ++i)
            {
                var c = text[i];
                bool haveO = o != null;
                if (remove.Contains(c))
                {
                    if (haveO)
                        continue;
                    o = text.ToCharArray();
                    d = i;
                    continue;
                }
                if (haveO)
                {
                    o[d] = c;
                    ++d;
                }
            }
            return o == null ? text : new String(o, 0, d);
        }

    }



}
