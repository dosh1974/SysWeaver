using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace SysWeaver
{
    /// <summary>
    /// An equality comparer for byte array content.
    /// Null arrays are supported by Equals (two null arrays are equal), use <see cref="FastByteArrayEqualityComparer"/> if arrays are never null and always at least 4 bytes long.
    /// </summary>
    public sealed class ByteArrayEqualityComparer : IEqualityComparer<Byte[]>
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
        /// The hash code is built from (up to) the first 4 bytes of the array (big endian), arrays with the same content always have the same hash code.
        /// </summary>
        /// <param name="obj">The array to get the hash code for</param>
        /// <returns>The hash code, 0 for an empty array</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="obj"/> is null</exception>
        public int GetHashCode([DisallowNull] byte[] obj)
        {
            ArgumentNullException.ThrowIfNull(obj);
            var l = obj.Length;
            if (l >= 4)
                return BinaryPrimitives.ReadInt32BigEndian(obj);
            switch (l)
            {
                case 1:
                    return obj[0];
                case 2:
                    return (((int)obj[0]) << 8) | (int)obj[1];
                case 3:
                    return (((int)obj[0]) << 16) | (((int)obj[1]) << 8) | (int)obj[2];
                default:
                    return 0;
            }
        }
    }

}
