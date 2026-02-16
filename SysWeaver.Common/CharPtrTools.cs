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
            fixed (Char* ss = s)
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

        public static Char* IndexOfAny(char c0, char c1, Char* start, Char* end)
        {
            while (start < end)
            {
                var c = *start;
                if ((c == c0) || (c == c1))
                    return start;
                ++start;
            }
            return null;
        }

        public static Char* IndexOfAny(char c0, char c1, char c2, Char* start, Char* end)
        {
            while (start < end)
            {
                var c = *start;
                if ((c == c0) || (c == c1) || (c == c2))
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
            => String.Create(len, (IntPtr)start, CreateLowerCasedString);


        /// <summary>
        /// Create an uppercased string (using the invariant culture).
        /// With zero unnecessary memory allocations and zero unnecessary memory copying.
        /// </summary>
        /// <param name="start"></param>
        /// <param name="len"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String ToUpperCaseString(Char* start, int len)
            => String.Create(len, (IntPtr)start, CreateUpperCasedString);


        public static readonly TextInfo Ti = CultureInfo.InvariantCulture.TextInfo;


        public static readonly Func<Char, Char> ToLower = Ti.ToLower;
        public static readonly Func<Char, Char> ToUpper = Ti.ToUpper;

        /// <summary>
        /// Copy some memory to a lowercased version
        /// </summary>
        /// <param name="dest"></param>
        /// <param name="source"></param>
        /// <param name="length"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyLowerCased(Char* dest, Char* source, int length)
        {
            while (length > 0)
            {
                --length;
                dest[length] = Char.ToLowerInvariant(source[length]);
            }
        }

        /// <summary>
        /// Copy some memory to an uppercased version
        /// </summary>
        /// <param name="dest"></param>
        /// <param name="source"></param>
        /// <param name="length"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyUpperCased(Char* dest, Char* source, int length)
        {
            while (length > 0)
            {
                --length;
                dest[length] = Char.ToUpperInvariant(source[length]);
            }
        }


        #region String creators

        static readonly SpanAction<Char, IntPtr> CreateLowerCasedString = (to, src) =>
        {
            fixed (Char* d = to)
            {
                var source = (Char*)src.ToPointer();
                var length = to.Length;
                while (length > 0)
                {
                    --length;
                    d[length] = Char.ToLowerInvariant(source[length]);
                }
            }
        };


        static readonly SpanAction<Char, IntPtr> CreateUpperCasedString= (to, src) =>
        {
            fixed (Char* d = to)
            {
                var source = (Char*)src.ToPointer();
                var length = to.Length;
                while (length > 0)
                {
                    --length;
                    d[length] = Char.ToUpperInvariant(source[length]);
                }
            }
        };



        #endregion//String creators


    }


}
