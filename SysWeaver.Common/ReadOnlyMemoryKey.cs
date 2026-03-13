using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SysWeaver
{

    public static class ReadOnlyMemoryKey
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IReadOnlyMemoryKey<Char> Create(String s) => new StringMemoryKey(s);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IReadOnlyMemoryKey<Char> Create(String s, int offset, int length = -1) => new MemoryKey<Char>(s.AsMemory().Slice(offset, length < 0 ? (s.Length - offset) : length));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IReadOnlyMemoryKey<T> Create<T>(ReadOnlyMemory<T> m) where T : struct => new MemoryKey<T>(m);


        /// <summary>
        /// Only use when the key is hashes (random data) and have a length greater than 4
        /// </summary>
        public static readonly IEqualityComparer<IReadOnlyMemoryKey<Char>> HashStringEqualityComparer = new ImplHashStringEqualityComparer();

        sealed class ImplHashStringEqualityComparer : IEqualityComparer<IReadOnlyMemoryKey<Char>>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Equals(IReadOnlyMemoryKey<Char> x, IReadOnlyMemoryKey<Char> y)
                => x.Span.SequenceEqual(y.Span);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int GetHashCode([DisallowNull] IReadOnlyMemoryKey<Char> obj)
            {
                var t = MemoryMarshal.Cast<Char, ulong>(obj.Span)[0];
                var r = (t >> 24) | (t << 24);
                t ^= r;
                return (int)t;
            }

        }

    }


    public interface IReadOnlyMemoryKey<T> where T : struct
    {
        ReadOnlySpan<T> Span { get; }
    }


    public static class ReadOnlyMemoryKey<T> where T : struct
    {
        public static readonly IEqualityComparer<IReadOnlyMemoryKey<T>> EqualityComparer = new ImplEqualityComparer();

        sealed class ImplEqualityComparer : IEqualityComparer<IReadOnlyMemoryKey<T>>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Equals(IReadOnlyMemoryKey<T> x, IReadOnlyMemoryKey<T> y)
                => x.Span.SequenceEqual(y.Span);

            public unsafe int GetHashCode([DisallowNull] IReadOnlyMemoryKey<T> obj)
            {
                var o = MemoryMarshal.Cast<T, Byte>(obj.Span);
                int hash1 = (5381 << 16) + 5381;
                var len = o.Length;
                int hash2 = hash1;
                fixed (Byte* src = o)
                {
                    // 32 bit machines.
                    int* pint = (int*)src;
                    while (len >= 8)
                    {
                        hash1 = ((hash1 << 5) + hash1 + (hash1 >> 27)) ^ pint[0];
                        hash2 = ((hash2 << 5) + hash2 + (hash2 >> 27)) ^ pint[1];
                        pint += 2;
                        len -= 8;
                    }
                    byte* t = (byte*)pint;
                    while (len > 0)
                    {
                        hash1 = ((hash1 << 5) + hash1 + (hash1 >> 27)) ^ t[0];
                        ++t;
                        --len;
                    }
                }
                return hash1 + (hash2 * 1566083941);
            }
        }
    }


    struct StringMemoryKey : IReadOnlyMemoryKey<Char>
    {
        public StringMemoryKey(String s)
        {
            S = s;
        }

        public ReadOnlySpan<Char> Span => S.AsSpan();

        public readonly String S;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString() => S;
    }


    struct MemoryKey<T> : IReadOnlyMemoryKey<T> where T : struct
    {
        public MemoryKey(ReadOnlyMemory<T> m)
        {
            M = m;
        }
        public ReadOnlySpan<T> Span => M.Span;

        public readonly ReadOnlyMemory<T> M;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString() => M.ToString();

    }




}
