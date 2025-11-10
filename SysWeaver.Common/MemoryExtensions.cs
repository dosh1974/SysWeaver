using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace SysWeaver
{

    public static class MemoryExtensions
    {


        /// <summary>
        /// Converts some data into a hexadecimal string
        /// </summary>
        /// <param name="bytes">The data</param>
        /// <returns>A hexadecimal string</returns>
        public static String ToHex(this ReadOnlyMemory<Byte> bytes) => String.Create(bytes.Length * 2, bytes, WriteHexAction);


        static readonly Char[] ToHexDigits = "0123456789abcdef".ToCharArray();

        static void WriteHex(Span<Char> to, ReadOnlyMemory<Byte> data)
        {
            var ch = ToHexDigits;
            var s = data.Span;
            var l = data.Length;
            int o = 0;            
            for (int i = 0; i < l; ++ i)
            {
                var b = s[i];
                var t = b;
                b >>= 4;
                t &= 0xf;
                to[o] = ch[b];
                ++o;
                to[o] = ch[t];
                ++o;
            }
        }

        internal static readonly SpanAction<Char, ReadOnlyMemory<Byte>> WriteHexAction = WriteHex;


    }

    public static class ReadOnlyMemoryComparer
    {

        sealed class Cmp<T> : IComparer<ReadOnlyMemory<T>>, IEqualityComparer<ReadOnlyMemory<T>>
        {
            public static readonly Cmp<T> Instance = new Cmp<T>();

            public unsafe int Compare(ReadOnlyMemory<T> x, ReadOnlyMemory<T> y)
            {
                var l = x.Length;
                var c = l - y.Length;
                if (c != 0)
                    return c;
                using var px = x.Pin();
                using var py = y.Pin();
                var dx = (Byte*)px.Pointer;
                var dy = (Byte*)py.Pointer;
                l *= Marshal.SizeOf<T>();
                for (int i = 0; i < l; ++ i)
                {
                    c = dx[i] - dy[i];
                    if (c != 0)
                        return c;
                }
                return 0;
            }

            public bool Equals(ReadOnlyMemory<T> x, ReadOnlyMemory<T> y)
                => x.Span.SequenceEqual(y.Span);

            public unsafe int GetHashCode([DisallowNull] ReadOnlyMemory<T> obj)
            {
                using var pm = obj.Pin();
                var s = (Byte*)pm.Pointer;
                var t = new ReadOnlySpan<Byte>(s, obj.Length * Marshal.SizeOf<T>());
                return GxHash.Hash32(t, 12);
            }

        }



        public static IComparer<ReadOnlyMemory<T>> GetComparer<T>() => Cmp<T>.Instance;
        
        public static IEqualityComparer<ReadOnlyMemory<T>> GetEqualityComparer<T>() => Cmp<T>.Instance;

    }

    public static class MemoryComparer
    {

        sealed class Cmp<T> : IComparer<Memory<T>>, IEqualityComparer<Memory<T>>
        {
            public static readonly Cmp<T> Instance = new Cmp<T>();

            public unsafe int Compare(Memory<T> x, Memory<T> y)
            {
                var l = x.Length;
                var c = l - y.Length;
                if (c != 0)
                    return c;
                using var px = x.Pin();
                using var py = y.Pin();
                var dx = (Byte*)px.Pointer;
                var dy = (Byte*)py.Pointer;
                l *= Marshal.SizeOf<T>();
                for (int i = 0; i < l; ++i)
                {
                    c = dx[i] - dy[i];
                    if (c != 0)
                        return c;
                }
                return 0;
            }

            public bool Equals(Memory<T> x, Memory<T> y)
                => x.Span.SequenceEqual(y.Span);

            public unsafe int GetHashCode([DisallowNull] Memory<T> obj)
            {
                using var pm = obj.Pin();
                var s = (Byte*)pm.Pointer;
                var t = new Span<Byte>(s, obj.Length * Marshal.SizeOf<T>());
                return GxHash.Hash32(t, 12);
            }

        }



        public static IComparer<Memory<T>> GetComparer<T>() => Cmp<T>.Instance;

        public static IEqualityComparer<Memory<T>> GetEqualityComparer<T>() => Cmp<T>.Instance;

    }

}
