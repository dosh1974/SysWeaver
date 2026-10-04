using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace SysWeaver
{

    public static class MemoryExtensions
    {


        /// <summary>
        /// Converts some data into a hexadecimal string
        /// </summary>
        /// <param name="bytes">The data</param>
        /// <returns>A hexadecimal string (lower case)</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static String ToHex(this ReadOnlyMemory<Byte> bytes)
            => Convert.ToHexStringLower(bytes.Span);


        /// <summary>
        /// Decode some text data into lines (see StringTools.GetLines)
        /// </summary>
        /// <param name="bytes">The data, a byte order mark (the preamble of the encoding) is removed</param>
        /// <param name="encoding">The text encoding, defaults to UTF8</param>
        /// <param name="trim">True to trim whitespaces from every line</param>
        /// <param name="removeEmpty">True to remove empty lines</param>
        /// <returns></returns>
        [SkipLocalsInit]
        public static String[] ToStringArray(this ReadOnlySpan<Byte> bytes, Encoding encoding = null, bool trim = false, bool removeEmpty = false)
        {
            encoding ??= Encoding.UTF8;
            var preamble = encoding.Preamble;
            if ((preamble.Length > 0) && bytes.StartsWith(preamble))
                bytes = bytes.Slice(preamble.Length);
            // The text is decoded to a temporary buffer (on the stack or pooled), only the lines are allocated
            var max = encoding.GetMaxCharCount(bytes.Length);
            if (max <= MaxStackChars)
            {
                Span<Char> buffer = stackalloc Char[max];
                var n = encoding.GetChars(bytes, buffer);
                return StringTools.GetLines(buffer.Slice(0, n), trim, removeEmpty);
            }
            var rented = ArrayPool<Char>.Shared.Rent(max);
            try
            {
                var n = encoding.GetChars(bytes, rented);
                return StringTools.GetLines(rented.AsSpan(0, n), trim, removeEmpty);
            }
            finally
            {
                ArrayPool<Char>.Shared.Return(rented);
            }
        }

        /// <summary>
        /// Temporary buffers up to this number of chars are allocated on the stack (4 KB)
        /// </summary>
        const int MaxStackChars = 2048;


        /// <summary>
        /// Write lower case hexadecimal digits (to must have room for 2 chars for every byte)
        /// </summary>
        static void WriteHex(Span<Char> to, ReadOnlyMemory<Byte> data)
            => Convert.TryToHexStringLower(data.Span, to, out _);

        internal static readonly SpanAction<Char, ReadOnlyMemory<Byte>> WriteHexAction = WriteHex;


    }

    /// <summary>
    /// Comparers for ReadOnlyMemory, the equality comparer compares the elements (using EqualityComparer.Default), the comparer is lexicographic (using Comparer.Default)
    /// </summary>
    public static class ReadOnlyMemoryComparer
    {

        sealed class Cmp<T> : IComparer<ReadOnlyMemory<T>>, IEqualityComparer<ReadOnlyMemory<T>>
        {
            public static readonly Cmp<T> Instance = new Cmp<T>();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int Compare(ReadOnlyMemory<T> x, ReadOnlyMemory<T> y)
                => MemoryCompare<T>.Compare(x.Span, y.Span);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Equals(ReadOnlyMemory<T> x, ReadOnlyMemory<T> y)
                => MemoryCompare<T>.Equals(x.Span, y.Span);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int GetHashCode([DisallowNull] ReadOnlyMemory<T> obj)
                => MemoryCompare<T>.GetHashCode(obj.Span);

        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IComparer<ReadOnlyMemory<T>> GetComparer<T>() => Cmp<T>.Instance;


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEqualityComparer<ReadOnlyMemory<T>> GetEqualityComparer<T>() => Cmp<T>.Instance;

    }

    /// <summary>
    /// Comparers for Memory, the equality comparer compares the elements (using EqualityComparer.Default), the comparer orders by length (shortest first) and then lexicographic (using Comparer.Default)
    /// </summary>
    public static class MemoryComparer
    {

        sealed class Cmp<T> : IComparer<Memory<T>>, IEqualityComparer<Memory<T>>
        {
            public static readonly Cmp<T> Instance = new Cmp<T>();

            public int Compare(Memory<T> x, Memory<T> y)
            {
                var c = x.Length - y.Length;
                if (c != 0)
                    return c;
                return MemoryCompare<T>.Compare(x.Span, y.Span);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Equals(Memory<T> x, Memory<T> y)
                => MemoryCompare<T>.Equals(x.Span, y.Span);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int GetHashCode([DisallowNull] Memory<T> obj)
                => MemoryCompare<T>.GetHashCode(obj.Span);

        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IComparer<Memory<T>> GetComparer<T>() => Cmp<T>.Instance;


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEqualityComparer<Memory<T>> GetEqualityComparer<T>() => Cmp<T>.Instance;

    }

    /// <summary>
    /// The implementation of the memory comparers
    /// </summary>
    static class MemoryCompare<T>
    {
        /// <summary>
        /// True if equal values always have the same bytes (and different values different bytes), so the bytes can be compared and hashed.
        /// True for the integer primitives, char, bool and enums (not for float / double, +0 and -0 are equal, all NaN are equal)
        /// </summary>
        static readonly bool Bitwise =
            (typeof(T) == typeof(Byte)) || (typeof(T) == typeof(SByte)) || (typeof(T) == typeof(Char)) || (typeof(T) == typeof(bool)) ||
            (typeof(T) == typeof(Int16)) || (typeof(T) == typeof(UInt16)) || (typeof(T) == typeof(Int32)) || (typeof(T) == typeof(UInt32)) ||
            (typeof(T) == typeof(Int64)) || (typeof(T) == typeof(UInt64)) || (typeof(T) == typeof(IntPtr)) || (typeof(T) == typeof(UIntPtr)) ||
            typeof(T).IsEnum;

        /// <summary>
        /// The bytes of the data (only valid if Bitwise)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ReadOnlySpan<Byte> Bytes(ReadOnlySpan<T> data)
            => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<T, Byte>(ref MemoryMarshal.GetReference(data)), data.Length * Unsafe.SizeOf<T>());

        /// <summary>
        /// The hash of empty data
        /// </summary>
        const int EmptyHash = 0x2f8b51c7;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Equals(ReadOnlySpan<T> x, ReadOnlySpan<T> y)
        {
            if (x.Length != y.Length)
                return false;
            if (Bitwise)
                return Bytes(x).SequenceEqual(Bytes(y));
            return x.SequenceEqual(y);
        }

        public static int GetHashCode(ReadOnlySpan<T> data)
        {
            if (Bitwise)
            {
                // GxHash reads a whole vector for short data (and the reference of empty data can be null)
                if (data.Length == 0)
                    return EmptyHash;
                return GxHash.Hash32(Bytes(data), 12);
            }
            // Combine the hashes of the elements (so that equal values always have the same hash)
            HashCode h = new();
            var cmp = EqualityComparer<T>.Default;
            foreach (var x in data)
                h.Add(x == null ? 0 : cmp.GetHashCode(x));
            h.Add(data.Length);
            return h.ToHashCode();
        }

        /// <summary>
        /// Lexicographic compare (using Comparer.Default for the elements, then the shorter is first)
        /// </summary>
        public static int Compare(ReadOnlySpan<T> x, ReadOnlySpan<T> y)
        {
            if ((typeof(T) == typeof(Byte)) || (typeof(T) == typeof(Char)))
            {
                // Most compares are decided by the first element, so check it before the (vectorized) compare
                if ((x.Length > 0) && (y.Length > 0))
                {
                    if (typeof(T) == typeof(Byte))
                    {
                        var a = Unsafe.As<T, Byte>(ref MemoryMarshal.GetReference(x));
                        var b = Unsafe.As<T, Byte>(ref MemoryMarshal.GetReference(y));
                        if (a != b)
                            return a - b;
                    }
                    else
                    {
                        var a = Unsafe.As<T, Char>(ref MemoryMarshal.GetReference(x));
                        var b = Unsafe.As<T, Char>(ref MemoryMarshal.GetReference(y));
                        if (a != b)
                            return a - b;
                    }
                }
                if (typeof(T) == typeof(Byte))
                    return Bytes(x).SequenceCompareTo(Bytes(y));
                return MemoryMarshal.Cast<Byte, Char>(Bytes(x)).SequenceCompareTo(MemoryMarshal.Cast<Byte, Char>(Bytes(y)));
            }
            var xl = x.Length;
            var yl = y.Length;
            var cl = xl < yl ? xl : yl;
            var cmp = Comparer<T>.Default;
            for (int i = 0; i < cl; ++i)
            {
                var c = cmp.Compare(x[i], y[i]);
                if (c != 0)
                    return c;
            }
            return xl - yl;
        }
    }

}
