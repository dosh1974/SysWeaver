using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace SysWeaver
{
    /// <summary>
    /// An equality comparer for byte array content
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

        public bool Equals(byte[] x, byte[] y)
        {
            if (x == y)
                return true;
            if ((x == null) || (y == null))
                return false;
            return SpanExt.ContentEqual(x.AsSpan(), y.AsSpan()); 
        }

        public int GetHashCode([DisallowNull] byte[] obj)
        {
            var l = obj.Length;
            switch (l)
            {
                case 0:
                    return 0;
                case 1:
                    return obj[0];
                case 2:
                    return (((int)obj[0]) << 8) | (int)obj[1];
                case 3:
                    return (((int)obj[0]) << 16) | (((int)obj[1]) << 8) | (int)obj[2];
                default:
                    return (((int)obj[0]) << 24) | (((int)obj[1]) << 16) | (((int)obj[2]) << 8) | (int)obj[3];
            }
        }
    }

}
