using System;
using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace SysWeaver
{

    /// <summary>
    /// Low level string handling with char pointers
    /// </summary>
    public unsafe static class CharPtrTools
    {

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Char* IndexOf(String s, Char* start, Char* end)
        {
            fixed (Char* ss = s.AsSpan())
                return IndexOf(ss, s.Length, start, end);
        }

        public static Char* IndexOf(Char* str, int strLen, Char* start, Char* end)
        {
            end -= strLen;
            while (start < end)
            {
                if ((*start) == *(str))
                {
                    int i;
                    for (i = 1; i < strLen; ++ i)
                    {
                        if (start[i] != str[i])
                            break;
                    }
                    if (i >= strLen)
                        return start;
                }
                ++start;
            }
            return null;
        }

        public static Char* IndexOf(Char f, Char* start, Char* end)
        {
            while (start < end)
            {
                if ((*start) == f)
                    return start;
                ++start;
            }
            return null;
        }

        public static Char* IndexOfAny(SearchValues<Char> vals, Char* start, Char* end)
        {
            while (start < end)
            {
                if (vals.Contains(*start))
                    return start;
                ++start;
            }
            return null;
        }

        /// <summary>
        /// Trim away whitespaces from a memory range
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        public static void Trim(ref Char* start, ref Char* end)
        {
            while (start < end)
            {
                if (!Char.IsWhiteSpace(*start))
                    break;
                ++start;
            }
            while (end > start)
            {
                --end;
                if (!Char.IsWhiteSpace(*end))
                {
                    ++end;
                    break;
                }
            }
        }

        /// <summary>
        /// Create a trimmed string with zero unnecessary memory allocations and zero unnecessary memory copying.
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String ToTrimmedString(Char* start, Char* end)
        {
            Trim(ref start, ref end);
            return new string(start, 0, (int)(end - start));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String ToString(Char* start, int len)
            => new string(start, 0, len);


        /// <summary>
        /// Create a lowercased string (using the invariant culture).
        /// With zero unnecessary memory allocations and zero unnecessary memory copying.
        /// </summary>
        /// <param name="start"></param>
        /// <param name="len"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String ToLowerCaseString(Char* start, int len)
            => String.Create(len, new IntPtr(start), CreateLowerCasedString);


        /// <summary>
        /// Create an uppercased string (using the invariant culture).
        /// With zero unnecessary memory allocations and zero unnecessary memory copying.
        /// </summary>
        /// <param name="start"></param>
        /// <param name="len"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String ToUpperCaseString(Char* start, int len)
            => String.Create(len, new IntPtr(start), CreateUpperCasedString);


        static readonly TextInfo Ti = CultureInfo.InvariantCulture.TextInfo;

        /// <summary>
        /// Copy some memory to a lowercased version
        /// </summary>
        /// <param name="dest"></param>
        /// <param name="source"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public static Char* CopyLowerCased(Char* dest, Char* source, int length)
        {
            var text = Ti;
            var end = source + length;
            while (source < end)
            {
                *dest = text.ToLower(*source);
                ++source;
                ++dest;
            }
            return dest;
        }

        /// <summary>
        /// Copy some memory to an uppercased version
        /// </summary>
        /// <param name="dest"></param>
        /// <param name="source"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public static Char* CopyUpperCased(Char* dest, Char* source, int length)
        {
            var text = Ti;
            var end = source + length;
            while (source < end)
            {
                *dest = text.ToUpper(*source);
                ++source;
                ++dest;
            }
            return dest;
        }


        #region String creators

        static readonly SpanAction<Char, IntPtr> CreateUpperCasedString= (to, src) =>
        {
            fixed (Char* d = to)
                CopyUpperCased(d, (Char*)src.ToPointer(), to.Length);
        };


        static readonly SpanAction<Char, IntPtr> CreateLowerCasedString = (to, src) =>
        {
            fixed (Char* d = to)
                CopyLowerCased(d, (Char*)src.ToPointer(), to.Length);
        };


        #endregion//String creators


    }


}
