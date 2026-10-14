using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace SysWeaver
{
    /// <summary>
    /// A fast equality comparer for byte array's that can be used if all these conditions are met:
    /// * Arrays may not be null.
    /// * Arrays must be at least 4 bytes in length.
    /// * The keys are random (ex: hashes): the hash code only depends on the length and the first and last 8 bytes (constant time).
    /// Supports lookups by <see cref="ReadOnlySpan{T}"/> of bytes (alternate lookups of Dictionary, ConcurrentDictionary, HashSet and <see cref="LowAllocConcurrentDictionary{TKey, TValue}"/>).
    /// </summary>
    public sealed class FastByteArrayEqualityComparer : IEqualityComparer<Byte[]>, IAlternateEqualityComparer<ReadOnlySpan<Byte>, Byte[]>
    {
        FastByteArrayEqualityComparer()
        {
        }

        /// <summary>
        /// A fast equality comparer for byte array's that can be used if all these conditions are met:
        /// * Arrays may not be null.
        /// * Arrays must be at least 4 bytes in length.
        /// * The keys are random (ex: hashes): the hash code only depends on the length and the first and last 8 bytes (constant time).
        /// </summary>
        public static readonly IEqualityComparer<Byte[]> Instance = new FastByteArrayEqualityComparer();

        /// <summary>
        /// Determines whether two byte arrays have the same content (same length and the same bytes)
        /// </summary>
        /// <param name="x">The first array, may not be null (a null array is treated as an empty array)</param>
        /// <param name="y">The second array, may not be null (a null array is treated as an empty array)</param>
        /// <returns>True if the arrays have the same content, else false</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(byte[] x, byte[] y)
            => SpanExt.ContentEqual(x.AsSpan(), y.AsSpan());

        /// <summary>
        /// Get a hash code for the content of a byte array: a hash of the length and the first and last 8 bytes (constant time, a fast seeded hash).
        /// Arrays with the same content always have the same hash code; arrays of the same length that only differ in the bytes between the first and last 8 bytes
        /// also have the same hash code (use <see cref="ByteArrayEqualityComparer"/> for such keys, it hashes all bytes).
        /// The hash code is randomized per process (like string hash codes), never persist it.
        /// </summary>
        /// <param name="obj">The array to get the hash code for, must be at least 4 bytes long</param>
        /// <returns>The hash code</returns>
        /// <exception cref="ArgumentOutOfRangeException">If <paramref name="obj"/> is shorter than 4 bytes (or null)</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetHashCode([DisallowNull] byte[] obj)
        {
            if ((obj == null) || (obj.Length < 4))
                ThrowTooShort();
            return HashSpan(obj);
        }

        [DoesNotReturn]
        static void ThrowTooShort() => throw new ArgumentOutOfRangeException("obj", "The array must be at least 4 bytes long");

        /// <summary>
        /// The hash code of some bytes (any length), the same as <see cref="GetHashCode(byte[])"/> for an array with the same content
        /// </summary>
        /// <param name="bytes">The bytes</param>
        /// <returns>The hash code</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int HashSpan(ReadOnlySpan<Byte> bytes)
        {
            var h = LowAllocStringHash.GetEndsHash(bytes);
            return (int)h ^ (int)(h >> 32);
        }

        /// <summary>
        /// Determines whether a span has the same content as a byte array
        /// </summary>
        /// <param name="alternate">The bytes</param>
        /// <param name="other">The array, may not be null (a null array is treated as an empty array)</param>
        /// <returns>True if the array has the same content</returns>
        public bool Equals(ReadOnlySpan<Byte> alternate, Byte[] other)
            => SpanExt.ContentEqual(alternate, other.AsSpan());

        /// <summary>
        /// Get a hash code for the bytes, the same as for an array with the same content.
        /// Spans shorter than 4 bytes are hashed too (no exception), such keys can't be in a collection using this comparer.
        /// </summary>
        /// <param name="alternate">The bytes</param>
        /// <returns>The hash code</returns>
        public int GetHashCode(ReadOnlySpan<Byte> alternate) => HashSpan(alternate);

        /// <summary>
        /// Create an array with the bytes (used when an alternate lookup adds a key)
        /// </summary>
        /// <param name="alternate">The bytes</param>
        /// <returns>A new array</returns>
        public Byte[] Create(ReadOnlySpan<Byte> alternate) => alternate.ToArray();
    }

}
