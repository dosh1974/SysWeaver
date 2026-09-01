using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

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




    public static class ExceptionExt
    {
        
        static ExceptionExt()
        {
            var type = typeof(Exception);
            var fieldInfo = type.GetField("_message", BindingFlags.Instance | BindingFlags.NonPublic);
            var exp = Expression.Variable(typeof(Exception), "ex");
            var textp = Expression.Variable(typeof(String), "text");
            InternalSetText = Expression.Lambda<Action<Exception, String>>(Expression.Assign(Expression.Field(exp, fieldInfo), textp), exp, textp).Compile(); 
        }

        static readonly Action<Exception, String> InternalSetText;


        /// <summary>
        /// Set a new text message on an exception.
        /// Uses reflection and internal fields, so it may break in future versions of .NET.
        /// </summary>
        /// <param name="ex">The exception to set a new message text on</param>
        /// <param name="newException">The new text</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetMessage(this Exception ex, String newException)
            => InternalSetText(ex, newException);
    }


}
