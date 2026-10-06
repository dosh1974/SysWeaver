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
    /// </summary>
    public sealed class FastByteArrayEqualityComparer : IEqualityComparer<Byte[]>
    {
        FastByteArrayEqualityComparer()
        {
        }

        /// <summary>
        /// A fast equality comparer for byte array's that can be used if all these conditions are met:
        /// * Arrays may not be null.
        /// * Arrays must be at least 4 bytes in length.
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
        /// Get a hash code for the content of a byte array, this is the first 4 bytes of the array as an Int32 (machine endian)
        /// </summary>
        /// <param name="obj">The array to get the hash code for, must be at least 4 bytes long</param>
        /// <returns>The hash code</returns>
        /// <exception cref="ArgumentOutOfRangeException">If <paramref name="obj"/> is shorter than 4 bytes (or null)</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetHashCode([DisallowNull] byte[] obj) => BitConverter.ToInt32(obj);
    }

}
