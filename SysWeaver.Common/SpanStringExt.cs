using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace SysWeaver
{

    /// <summary>
    /// Low level string functions for char spans
    /// </summary>
    public static unsafe class SpanStringExt
    {
        /// <summary>
        /// Create an lowercase string from a span
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String ToLowerCaseString(this ReadOnlySpan<Char> text)
            => String.Create(text.Length, text, CreateLowerCasedString);

        /// <summary>
        /// Create an uppercase string from a span
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String ToUpperCaseString(this ReadOnlySpan<Char> text)
            => String.Create(text.Length, text, CreateUpperCasedString);


        /// <summary>
        /// Concat spans into a string with zero unnecessary memory allocations and zero unnecessary memory copying.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static String ConcatToString(this ReadOnlySpan<char> a, ReadOnlySpan<char> b)
        {
            var al = a.Length;
            var bl = b.Length;
            fixed (Char* aa = a)
            fixed (Char* bb = b)
            {
                Span<(IntPtr, int)> x = stackalloc (IntPtr, int)[]
                {
                    (new IntPtr(aa), al),
                    (new IntPtr(bb), bl)
                };
                return String.Create(al + bl, x, Concat);
            }
        }

        /// <summary>
        /// Concat spans into a string with zero unnecessary memory allocations and zero unnecessary memory copying.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        /// <returns></returns>
        public static String ConcatToString(this ReadOnlySpan<char> a, ReadOnlySpan<char> b, ReadOnlySpan<char> c)
        {
            var al = a.Length;
            var bl = b.Length;
            var cl = c.Length;
            fixed (Char* aa = a)
            fixed (Char* bb = b)
            fixed (Char* cc = c)
            {
                Span<(IntPtr, int)> x = stackalloc (IntPtr, int)[]
                {
                    (new IntPtr(aa), al),
                    (new IntPtr(bb), bl),
                    (new IntPtr(cc), cl)
                };
                return String.Create(al + bl + cl, x, Concat);
            }
        }

        /// <summary>
        /// Concat spans into a string with zero unnecessary memory allocations and zero unnecessary memory copying.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        /// <param name="d"></param>
        /// <returns></returns>
        public static String ConcatToString(this ReadOnlySpan<char> a, ReadOnlySpan<char> b, ReadOnlySpan<char> c, ReadOnlySpan<char> d)
        {
            var al = a.Length;
            var bl = b.Length;
            var cl = c.Length;
            var dl = d.Length;
            fixed (Char* aa = a)
            fixed (Char* bb = b)
            fixed (Char* cc = c)
            fixed (Char* dd = d)
            {
                Span<(IntPtr, int)> x = stackalloc (IntPtr, int)[]
                {
                    (new IntPtr(aa), al),
                    (new IntPtr(bb), bl),
                    (new IntPtr(cc), cl),
                    (new IntPtr(dd), dl)
                };
                return String.Create(al + bl + cl + dl, x, Concat);
            }
        }


        static readonly SpanAction<Char, Span<(IntPtr, int)>> Concat = (dest, sources) =>
        {
            fixed (Char* dd = dest)
            {
                var d = dd;
                var l = sources.Length;
                for (int i = 0; i < l; ++i)
                {
                    var len = sources[i].Item2;
                    var s = (Char*)sources[i].Item1.ToPointer();
                    var l2 = len + len;
                    Buffer.MemoryCopy(s, d, l2, l2);
                    d += len;
                }
            }
        };


        static readonly SpanAction<Char, ReadOnlySpan<Char>> CreateUpperCasedString = (to, src) =>
        {
            var t = CharPtrTools.Ti;
            var l = to.Length;
            while (l > 0)
            {
                --l;
                to[l] = t.ToUpper(src[l]);
            }
        };


        static readonly SpanAction<Char, ReadOnlySpan<Char>> CreateLowerCasedString = (to, src) =>
        {
            var t = CharPtrTools.Ti;
            var l = to.Length;
            while (l > 0)
            {
                --l;
                to[l] = t.ToLower(src[l]);
            }
        };



    }


}
