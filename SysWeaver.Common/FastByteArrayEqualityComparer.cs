using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

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

        public bool Equals(byte[] x, byte[] y)
            => SpanExt.ContentEqual(x.AsSpan(), y.AsSpan());

        public int GetHashCode([DisallowNull] byte[] obj) => BitConverter.ToInt32(obj);
    }

}
