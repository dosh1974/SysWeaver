using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SysWeaver
{

    /// <summary>
    /// Class that takes some data and represents it as a compact string.
    /// An integer is written in base N (where N is the number of valid chars), least significant digit first, using the ASCII chars [33, 127] (printable chars and DEL) that aren't invalid.
    /// </summary>
    /// <remarks>
    /// Immutable and thread safe. Typically used through <see cref="Default"/> or <see cref="Secure"/>.
    /// The encoding of a value is not padded, so the length of the string depends on the value (0 is a single char).
    /// The decoding is not canonical: many strings decode to the same value (trailing zero digits are ignored and too long strings wrap around),
    /// so compare the strings or the decoded values, but don't assume that Encode(Decode(s)) == s for strings that weren't made by Encode.
    /// </remarks>
    public sealed class CompactAsciiString
    {

        /// <summary>
        /// Chars that are escaped in common formats (json, sql, paths etc), DEL (127) is escaped by json serializers.
        /// The default invalid set of the <see cref="CompactAsciiString(IReadOnlySet{char})"/> constructor
        /// </summary>
        public static readonly IReadOnlySet<Char> InvalidDefaults = ReadOnlyData.Set("\\/'\"´`|%_\x7F".ToCharArray());

        /// <summary>
        /// Uses all ASCII chars [33, 126] except the <see cref="InvalidDefaults"/> (base 86).
        /// The chars don't expand "in transit" with relaxed escaping, such as json strings written by SwJson (or System.Text.Json using <see cref="System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping"/>), sql requests etc.
        /// </summary>
        /// <remarks>
        /// The default System.Text.Json encoder (and html / xml) escapes &amp;, +, &lt; and &gt;, use <see cref="Secure"/> for values that are written that way.
        /// </remarks>
        public static readonly CompactAsciiString Default = new CompactAsciiString();

        /// <summary>
        /// The alphabet of <see cref="Secure"/>, the index is the digit value
        /// </summary>
        const String SecureChars = "-.0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

        /// <summary>
        /// Only uses chars that can be used "everywhere" without escaping: uri's (data and url encoding), xml/html attributes and values, js strings, json etc.
        /// The chars are '-', '.', '0'-'9', 'A'-'Z' and 'a'-'z' (base 64).
        /// </summary>
        /// <remarks>
        /// The alphabet is fixed (not computed from the escape functions of the framework), so encoded values never change.
        /// </remarks>
        public static readonly CompactAsciiString Secure = new CompactAsciiString(GetInvalid(SecureChars));

        /// <summary>
        /// Get the invalid set for an alphabet: every ASCII char [33, 127] that isn't in <paramref name="valid"/>
        /// </summary>
        static HashSet<Char> GetInvalid(String valid)
        {
            var c = new HashSet<char>();
            for (int i = 33; i < 128; ++ i)
            {
                var t = (Char)i;
                if (valid.IndexOf(t) < 0)
                    c.Add(t);
            }
            return c;
        }

        /// <summary>
        /// Create a compact string encoder / decoder.
        /// The alphabet is all chars in the [33, 127] range (printable ASCII excluding space) that aren't in the <paramref name="invalid"/> set.
        /// </summary>
        /// <param name="invalid">The chars that must not be used, null to use <see cref="InvalidDefaults"/> (chars outside of the [33, 127] range are ignored)</param>
        /// <exception cref="ArgumentException">Less than 2 chars remain valid</exception>
        public CompactAsciiString(IReadOnlySet<Char> invalid = null)
        {
            invalid = invalid ?? InvalidDefaults;
            List<Char> valid = new List<char>(128);
            Dictionary<Char, uint> vals = new Dictionary<char, uint>();
            for (int i = 33; i < 128; ++ i)
            {
                var c = (Char)i;
                if (invalid.Contains(c))
                    continue;
                vals.Add(c, (uint)valid.Count);
                valid.Add(c);
            }
            if (valid.Count < 2)
                throw new ArgumentException("At least 2 chars must be valid", nameof(invalid));
            var va = valid.ToArray();
            Valid = va;
            Values = vals.Freeze();
            ValidChars = va;
            Base = (uint)va.Length;
            var lookup = new byte[128];
            Array.Fill(lookup, InvalidChar);
            for (int i = 0; i < va.Length; ++i)
                lookup[va[i]] = (byte)i;
            Lookup = lookup;
        }

        /// <summary>
        /// The value in <see cref="Lookup"/> for chars that aren't valid
        /// </summary>
        const byte InvalidChar = 0xff;

        /// <summary>
        /// Same as Valid (but faster)
        /// </summary>
        readonly Char[] ValidChars;

        /// <summary>
        /// The base (number of valid chars)
        /// </summary>
        readonly uint Base;

        /// <summary>
        /// Same as Values (but faster), indexed by the char, InvalidChar if not valid
        /// </summary>
        readonly Byte[] Lookup;

        /// <summary>
        /// Throw the exception for an invalid char (not inlined, so that the callers stays small)
        /// </summary>
        /// <exception cref="KeyNotFoundException">Always</exception>
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ThrowInvalidChar(Char c)
            => throw new KeyNotFoundException("The char '" + c + "' (" + (int)c + ") is not valid in a compact string!");

        /// <summary>
        /// Get the digit value of a char in a compact string
        /// </summary>
        /// <exception cref="KeyNotFoundException">The char isn't valid</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        uint GetValue(String compactString, int index)
        {
            uint c = compactString[index];
            uint v = c < 128 ? Lookup[c] : InvalidChar;
            if (v == InvalidChar)
                ThrowInvalidChar((Char)c);
            return v;
        }

#if DEBUG

        static CompactAsciiString()
        {
            var t = Default;
            if (t.DecodeUInt64(t.Encode(UInt64.MinValue)) != UInt64.MinValue)
                throw new Exception("Internal error!");
            if (t.DecodeUInt64(t.Encode(UInt64.MaxValue)) != UInt64.MaxValue)
                throw new Exception("Internal error!");
            if (t.DecodeUInt32(t.Encode(UInt32.MinValue)) != UInt32.MinValue)
                throw new Exception("Internal error!");
            if (t.DecodeUInt32(t.Encode(UInt32.MaxValue)) != UInt32.MaxValue)
                throw new Exception("Internal error!");
            if (t.DecodeInt64(t.Encode(Int64.MinValue)) != Int64.MinValue)
                throw new Exception("Internal error!");
            if (t.DecodeInt64(t.Encode(Int64.MaxValue)) != Int64.MaxValue)
                throw new Exception("Internal error!");
            if (t.DecodeInt32(t.Encode(Int32.MinValue)) != Int32.MinValue)
                throw new Exception("Internal error!");
            if (t.DecodeInt32(t.Encode(Int32.MaxValue)) != Int32.MaxValue)
                throw new Exception("Internal error!");


            t = Secure;
            if (new String(t.ValidChars) != SecureChars)
                throw new Exception("Internal error!");
            if (t.DecodeUInt64(t.Encode(UInt64.MinValue)) != UInt64.MinValue)
                throw new Exception("Internal error!");
            if (t.DecodeUInt64(t.Encode(UInt64.MaxValue)) != UInt64.MaxValue)
                throw new Exception("Internal error!");
            if (t.DecodeUInt32(t.Encode(UInt32.MinValue)) != UInt32.MinValue)
                throw new Exception("Internal error!");
            if (t.DecodeUInt32(t.Encode(UInt32.MaxValue)) != UInt32.MaxValue)
                throw new Exception("Internal error!");
            if (t.DecodeInt64(t.Encode(Int64.MinValue)) != Int64.MinValue)
                throw new Exception("Internal error!");
            if (t.DecodeInt64(t.Encode(Int64.MaxValue)) != Int64.MaxValue)
                throw new Exception("Internal error!");
            if (t.DecodeInt32(t.Encode(Int32.MinValue)) != Int32.MinValue)
                throw new Exception("Internal error!");
            if (t.DecodeInt32(t.Encode(Int32.MaxValue)) != Int32.MaxValue)
                throw new Exception("Internal error!");

        }




#endif//DEBUG

        /// <summary>
        /// Encode a 64-bit signed integer (the bits are encoded as an unsigned integer, so negative values use the most chars).
        /// 32-bit integers are implicitly converted (a negative Int32 is sign extended, use <see cref="DecodeInt32(string)"/> to decode it).
        /// </summary>
        /// <param name="value">The value to encode</param>
        /// <returns>The compact string that represents it</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public String Encode(Int64 value)
            => Encode((UInt64)value);

        /// <summary>
        /// Encode a 64-bit unsigned integer.
        /// </summary>
        /// <param name="value">The value to encode</param>
        /// <returns>The compact string that represents it (least significant digit first, at least one char)</returns>
        [SkipLocalsInit]
        public String Encode(UInt64 value)
        {
            var v = ValidChars;
            ulong vl = Base;
            // A 64 bit value needs at most 64 digits (base 2)
            Span<Char> temp = stackalloc char[64];
            for (int i = 0; ;)
            {
                var n = value / vl;
                var vi = value - (n * vl);
                temp[i] = v[(int)vi];
                ++i;
                if (n == 0)
                    return new string(temp.Slice(0, i));
                value = n;
            }
        }

        /// <summary>
        /// Decode a compact string to the 64-bit signed integer that it represents.
        /// </summary>
        /// <param name="compactString">The compact value representation</param>
        /// <returns>The value that was represented by the string</returns>
        /// <remarks>Not canonical and no overflow checks, see <see cref="DecodeUInt64(string)"/></remarks>
        /// <exception cref="ArgumentNullException"><paramref name="compactString"/> is null (debug builds only; release builds throw a <see cref="NullReferenceException"/>)</exception>
        /// <exception cref="KeyNotFoundException"><paramref name="compactString"/> contains a char that isn't valid (not in <see cref="Valid"/>)</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Int64 DecodeInt64(String compactString)
            => (Int64)DecodeUInt64(compactString);

        /// <summary>
        /// Decode a compact string to the 32-bit signed integer that it represents (the value is decoded as an unsigned 32-bit integer, so the encoding of a sign extended negative Int32 decodes correctly).
        /// </summary>
        /// <param name="compactString">The compact value representation</param>
        /// <returns>The value that was represented by the string</returns>
        /// <remarks>Not canonical and no overflow checks, see <see cref="DecodeUInt32(string)"/></remarks>
        /// <exception cref="ArgumentNullException"><paramref name="compactString"/> is null (debug builds only; release builds throw a <see cref="NullReferenceException"/>)</exception>
        /// <exception cref="KeyNotFoundException"><paramref name="compactString"/> contains a char that isn't valid (not in <see cref="Valid"/>)</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Int32 DecodeInt32(String compactString)
            => (Int32)DecodeUInt32(compactString);

        /// <summary>
        /// Decode a compact string to the 64-bit unsigned integer that it represents.
        /// </summary>
        /// <param name="compactString">The compact value representation</param>
        /// <returns>The value that was represented by the string, an empty string decodes to 0</returns>
        /// <remarks>No overflow checks are made, a string that represents a value larger than UInt64.MaxValue will wrap around.
        /// The decoding is not canonical: trailing (most significant) zero digits (<see cref="Valid"/>[0]) are ignored, so "s", "s" + Valid[0] etc decode to the same value.</remarks>
        /// <exception cref="ArgumentNullException"><paramref name="compactString"/> is null (debug builds only; release builds throw a <see cref="NullReferenceException"/>)</exception>
        /// <exception cref="KeyNotFoundException"><paramref name="compactString"/> contains a char that isn't valid (not in <see cref="Valid"/>)</exception>
        public UInt64 DecodeUInt64(String compactString)
        {
#if DEBUG
            ArgumentNullException.ThrowIfNull(compactString);
#endif//DEBUG
            ulong vl = Base;
            var l = compactString.Length;
            UInt64 r = 0;
            while (l > 0)
            {
                --l;
                r *= vl;
                r += GetValue(compactString, l);
            }
            return r;
        }

        /// <summary>
        /// Decode a compact string to the 32-bit unsigned integer that it represents (note that the return type is UInt64).
        /// </summary>
        /// <param name="compactString">The compact value representation</param>
        /// <returns>The value that was represented by the string (always in the [0, UInt32.MaxValue] range), an empty string decodes to 0</returns>
        /// <remarks>No overflow checks are made, a string that represents a value larger than UInt32.MaxValue will wrap around.
        /// The decoding is not canonical: trailing (most significant) zero digits (<see cref="Valid"/>[0]) are ignored, so "s", "s" + Valid[0] etc decode to the same value.</remarks>
        /// <exception cref="ArgumentNullException"><paramref name="compactString"/> is null (debug builds only; release builds throw a <see cref="NullReferenceException"/>)</exception>
        /// <exception cref="KeyNotFoundException"><paramref name="compactString"/> contains a char that isn't valid (not in <see cref="Valid"/>)</exception>
        public UInt64 DecodeUInt32(String compactString)
        {
#if DEBUG
            ArgumentNullException.ThrowIfNull(compactString);
#endif//DEBUG
            var vl = Base;
            var l = compactString.Length;
            UInt32 r = 0;
            while (l > 0)
            {
                --l;
                r *= vl;
                r += GetValue(compactString, l);
            }
            return r;
        }


        /// <summary>
        /// The valid chars (the alphabet), the index is the digit value
        /// </summary>
        public readonly IReadOnlyList<Char> Valid;

        /// <summary>
        /// The digit value of every valid char
        /// </summary>
        public readonly IReadOnlyDictionary<Char, uint> Values;


    }



}
