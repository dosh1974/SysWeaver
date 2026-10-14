using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace SysWeaver
{
    /// <summary>
    /// An equality comparer for byte array content.
    /// Null arrays are supported by Equals (two null arrays are equal), use <see cref="FastByteArrayEqualityComparer"/> if arrays are never null and always at least 4 bytes long.
    /// Supports lookups by <see cref="ReadOnlySpan{T}"/> of bytes (alternate lookups of Dictionary, ConcurrentDictionary, HashSet and <see cref="LowAllocConcurrentDictionary{TKey, TValue}"/>).
    /// </summary>
    public sealed class ByteArrayEqualityComparer : IEqualityComparer<Byte[]>, IAlternateEqualityComparer<ReadOnlySpan<Byte>, Byte[]>
    {
        ByteArrayEqualityComparer()
        {
        }

        /// <summary>
        /// An equality comparer for byte array content
        /// </summary>
        public static readonly IEqualityComparer<Byte[]> Instance = new ByteArrayEqualityComparer();

        /// <summary>
        /// Determines whether two byte arrays have the same content (same length and the same bytes)
        /// </summary>
        /// <param name="x">The first array, may be null</param>
        /// <param name="y">The second array, may be null</param>
        /// <returns>True if both arrays are null, the same instance or have the same content, else false</returns>
        public bool Equals(byte[] x, byte[] y)
        {
            if (x == y)
                return true;
            if ((x == null) || (y == null))
                return false;
            return SpanExt.ContentEqual(x.AsSpan(), y.AsSpan());
        }

        /// <summary>
        /// Get a hash code for the content of a byte array.
        /// All bytes are hashed (a fast seeded hash), arrays with the same content always have the same hash code.
        /// The hash code is randomized per process (like string hash codes), never persist it.
        /// </summary>
        /// <param name="obj">The array to get the hash code for</param>
        /// <returns>The hash code</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="obj"/> is null</exception>
        public int GetHashCode([DisallowNull] byte[] obj)
        {
            ArgumentNullException.ThrowIfNull(obj);
            return GetHashCode(obj.AsSpan());
        }

        /// <summary>
        /// Determines whether a span has the same content as a byte array
        /// </summary>
        /// <param name="alternate">The bytes</param>
        /// <param name="other">The array, may be null (never equal to a span)</param>
        /// <returns>True if the array isn't null and has the same content</returns>
        public bool Equals(ReadOnlySpan<Byte> alternate, Byte[] other)
            => (other != null) && SpanExt.ContentEqual(alternate, other.AsSpan());

        /// <summary>
        /// Get a hash code for the bytes, the same as for an array with the same content
        /// </summary>
        /// <param name="alternate">The bytes</param>
        /// <returns>The hash code</returns>
        public int GetHashCode(ReadOnlySpan<Byte> alternate) => HashSpan(alternate);

        /// <summary>
        /// The hash code of some bytes, the same as <see cref="GetHashCode(ReadOnlySpan{byte})"/> (a direct call, used by the alternate lookups of <see cref="LowAllocConcurrentDictionary{TKey, TValue}"/>)
        /// </summary>
        /// <param name="bytes">The bytes</param>
        /// <returns>The hash code</returns>
        internal static int HashSpan(ReadOnlySpan<Byte> bytes)
        {
            var h = LowAllocStringHash.GetHash(bytes);
            return (int)h ^ (int)(h >> 32);
        }

        /// <summary>
        /// Create an array with the bytes (used when an alternate lookup adds a key)
        /// </summary>
        /// <param name="alternate">The bytes</param>
        /// <returns>A new array</returns>
        public Byte[] Create(ReadOnlySpan<Byte> alternate) => alternate.ToArray();
    }

}
