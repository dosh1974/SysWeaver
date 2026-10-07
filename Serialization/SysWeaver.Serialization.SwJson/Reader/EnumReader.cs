using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace SysWeaver.Serialization.SwJson.Reader
{
    /// <summary>
    /// Fast enum parsing from UTF8: exact (case sensitive) names and plain integers, like Enum.Parse&lt;T&gt;(value, false).
    /// Anything else (flags combinations, white space etc) must be parsed by Enum.Parse.
    /// </summary>
    /// <remarks>The names and values are computed once per enum type (thread safe). Integers are limited to 18 digits and the range of the underlying type
    /// (<see cref="UInt64"/> values above <see cref="Int64.MaxValue"/> are left to Enum.Parse).</remarks>
    /// <typeparam name="T">The enum type</typeparam>
    static class EnumReader<T> where T : struct, Enum
    {
        static readonly Byte[][] NameBytes;
        static readonly T[] Values;
        static readonly long Min;
        static readonly long Max;

        static EnumReader()
        {
            var names = Enum.GetNames<T>();
            NameBytes = names.Select(x => Encoding.UTF8.GetBytes(x)).ToArray();
            Values = names.Select(x => Enum.Parse<T>(x)).ToArray();
            var u = Enum.GetUnderlyingType(typeof(T));
            if (u == typeof(Byte)) { Min = Byte.MinValue; Max = Byte.MaxValue; }
            else if (u == typeof(SByte)) { Min = SByte.MinValue; Max = SByte.MaxValue; }
            else if (u == typeof(UInt16)) { Min = UInt16.MinValue; Max = UInt16.MaxValue; }
            else if (u == typeof(Int16)) { Min = Int16.MinValue; Max = Int16.MaxValue; }
            else if (u == typeof(UInt32)) { Min = UInt32.MinValue; Max = UInt32.MaxValue; }
            else if (u == typeof(Int32)) { Min = Int32.MinValue; Max = Int32.MaxValue; }
            else if (u == typeof(UInt64)) { Min = 0; Max = Int64.MaxValue; }
            else { Min = Int64.MinValue; Max = Int64.MaxValue; }
        }

        /// <summary>
        /// Convert an integer to the enum (truncated to the size of the underlying type).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static T FromInt64(long v)
        {
            switch (Unsafe.SizeOf<T>())
            {
                case 1:
                    {
                        var b = (Byte)v;
                        return Unsafe.As<Byte, T>(ref b);
                    }
                case 2:
                    {
                        var b = (UInt16)v;
                        return Unsafe.As<UInt16, T>(ref b);
                    }
                case 4:
                    {
                        var b = (UInt32)v;
                        return Unsafe.As<UInt32, T>(ref b);
                    }
                default:
                    return Unsafe.As<long, T>(ref v);
            }
        }

        /// <summary>
        /// Parse an enum from UTF8 text (without quotes, unescaped), like a dictionary key: an exact name, an integer or anything that Enum.Parse&lt;T&gt;(value, false) accepts
        /// </summary>
        /// <param name="text">The UTF8 text</param>
        /// <returns>The value</returns>
        /// <exception cref="ArgumentException">The text isn't a valid value</exception>
        public static T Parse(ReadOnlySpan<Byte> text)
        {
            if (TryGet(text, out var value))
                return value;
            return Enum.Parse<T>(Encoding.UTF8.GetString(text), false);
        }

        /// <summary>
        /// Try to get the value of an exact name or a plain integer (within the range of the underlying type)
        /// </summary>
        /// <param name="text">The UTF8 text (without quotes)</param>
        /// <param name="value">The value if found, else default</param>
        /// <returns>True if the text is an exact name or an integer in range, false if the text must be parsed by Enum.Parse (that may still succeed)</returns>
        public static bool TryGet(ReadOnlySpan<Byte> text, out T value)
        {
            var l = text.Length;
            if (l <= 0)
            {
                value = default;
                return false;
            }
            var c = text[0];
            if (((uint)c - '0' > 9) && (c != '-'))
            {
                var names = NameBytes;
                for (int i = 0; i < names.Length; ++i)
                {
                    var n = names[i];
                    if ((n.Length == l) && text.SequenceEqual(n))
                    {
                        value = Values[i];
                        return true;
                    }
                }
                value = default;
                return false;
            }
            //  [-]digits, max 18 digits (fits a long)
            int o = c == '-' ? 1 : 0;
            if ((l == o) || (l - o > 18))
            {
                value = default;
                return false;
            }
            long v = 0;
            for (int i = o; i < l; ++i)
            {
                var d = (uint)text[i] - '0';
                if (d > 9)
                {
                    value = default;
                    return false;
                }
                v = v * 10 + d;
            }
            if (o != 0)
                v = -v;
            if ((v < Min) || (v > Max))
            {
                value = default;
                return false;
            }
            value = FromInt64(v);
            return true;
        }
    }
}
