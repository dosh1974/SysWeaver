using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SysWeaver
{
    /// <summary>
    /// SIMD helpers for converting the case of ASCII letters in strings
    /// </summary>
    static class AsciiCase
    {
        /// <summary>
        /// The first letter that must change when lower casing
        /// </summary>
        public const ushort ToLower = 'A';

        /// <summary>
        /// The first letter that must change when upper casing
        /// </summary>
        public const ushort ToUpper = 'a';

        /// <summary>
        /// The number of chars that are checked inline (for longer strings the rest is checked using the vectorized span searches of the runtime)
        /// </summary>
        const int InlineChars = 32;

        /// <summary>
        /// Check if a string may need a case change.
        /// The first chars are checked inline (no call overhead, most mixed case text has a letter to change early),
        /// only 128 bit vectors are used, allocating the converted string right after code using 256 bit vectors was measurably slower.
        /// </summary>
        /// <param name="str">The string to search</param>
        /// <param name="first">The first letter that must change, <see cref="ToLower"/> or <see cref="ToUpper"/></param>
        /// <returns>-1 if all chars are ASCII and none must change, else the index of an ASCII letter that must change, or the index of the first non ASCII char if no ASCII letter must change</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FindChangeOrNonAscii(String str, ushort first)
        {
            var l = str.Length;
            ref var s = ref Unsafe.As<char, ushort>(ref MemoryMarshal.GetReference(str.AsSpan()));
            if (Vector128.IsHardwareAccelerated && (l >= 8))
                return l >= InlineChars ? FindLong(str, ref s, first) : FindShort(ref s, l, first);
            for (nuint i = 0; i < (nuint)l; ++i)
            {
                uint c = Unsafe.Add(ref s, i);
                if (((c - first) < 26) || (c >= 0x80))
                    return (int)i;
            }
            return -1;
        }

        /// <summary>
        /// A mask with the bits set for the chars that must change or isn't ASCII
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Vector128<ushort> Match(Vector128<ushort> v, Vector128<ushort> a)
            => Vector128.LessThan(v - a, Vector128.Create((ushort)26)) | Vector128.GreaterThan(v, Vector128.Create((ushort)0x7f));

        /// <summary>
        /// 8 to 31 chars, one vector at a time (the last vector overlaps the previous one if the length isn't a multiple of 8)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int FindShort(ref ushort s, int l, ushort first)
        {
            var a = Vector128.Create(first);
            nuint i = 0;
            nuint last = (nuint)(l - 8);
            for (; ; )
            {
                var m = Match(Vector128.LoadUnsafe(ref s, i), a);
                if (m != Vector128<ushort>.Zero)
                    return (int)i + BitOperations.TrailingZeroCount(m.ExtractMostSignificantBits());
                if (i == last)
                    return -1;
                i += 8;
                if (i > last)
                    i = last;
            }
        }

        /// <summary>
        /// 32 chars or more: the first 32 chars inline (four vectors), the rest using the span searches of the runtime
        /// </summary>
        static int FindLong(String str, ref ushort s, ushort first)
        {
            var a = Vector128.Create(first);
            var m0 = Match(Vector128.LoadUnsafe(ref s, 0), a);
            var m1 = Match(Vector128.LoadUnsafe(ref s, 8), a);
            var m2 = Match(Vector128.LoadUnsafe(ref s, 16), a);
            var m3 = Match(Vector128.LoadUnsafe(ref s, 24), a);
            if (((m0 | m1) | (m2 | m3)) != Vector128<ushort>.Zero)
            {
                // One bit per char (32 bits)
                var bits = Vector128.Narrow(m0, m1).ExtractMostSignificantBits() | (Vector128.Narrow(m2, m3).ExtractMostSignificantBits() << 16);
                return BitOperations.TrailingZeroCount(bits);
            }
            var rest = str.AsSpan(InlineChars);
            var i = rest.IndexOfAnyInRange((char)first, (char)(first + 25));
            if (i < 0)
                i = rest.IndexOfAnyExceptInRange((char)0, (char)0x7f);
            return i < 0 ? -1 : InlineChars + i;
        }
    }
}
