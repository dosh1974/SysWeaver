using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace SysWeaver
{
    /// <summary>
    /// Exceptions are deemed equal if they are of the same type and have the same stack trace (message can be different, so that time or other state information doesn't make them differ).
    /// </summary>
    public sealed class ExceptionStackEqualityComparer : IEqualityComparer<Exception>
    {
        /// <summary>
        /// The only instance
        /// </summary>
        public static IEqualityComparer<Exception> Instance = new ExceptionStackEqualityComparer();

        ExceptionStackEqualityComparer()
        {
        }

        public bool Equals(Exception x, Exception y)
        {
            if (x == null)
                return y == null;
            if (y == null)
                return false;
            if (x.GetType() != y.GetType())
                return false;
            return x.StackTrace.FastEquals(y.StackTrace);
        }

        public int GetHashCode([DisallowNull] Exception obj)
            => obj?.StackTrace?.GetHashCode() ?? 0;
    }
}
