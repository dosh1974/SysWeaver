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
    /// <remarks>
    /// Typically used to de-duplicate logged / reported exceptions.
    /// Inner exceptions are not compared.
    /// Exceptions that were never thrown have a null <see cref="Exception.StackTrace"/>, so all never thrown exceptions of the same type are equal.
    /// </remarks>
    public sealed class ExceptionStackEqualityComparer : IEqualityComparer<Exception>
    {
        /// <summary>
        /// The only instance
        /// </summary>
        public static readonly IEqualityComparer<Exception> Instance = new ExceptionStackEqualityComparer();

        ExceptionStackEqualityComparer()
        {
        }

        /// <summary>
        /// Determines whether two exceptions are of the exact same runtime type and have the same (ordinal compare) stack trace.
        /// </summary>
        /// <param name="x">The first exception, may be null</param>
        /// <param name="y">The second exception, may be null</param>
        /// <returns>True if both are null, or if both have the same type and stack trace, else false</returns>
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

        /// <summary>
        /// Get a hash code based on the stack trace of an exception (the type isn't included).
        /// </summary>
        /// <param name="obj">The exception</param>
        /// <returns>The hash code of the stack trace, 0 if the exception or the stack trace is null</returns>
        /// <remarks>String hash codes are randomized per process, so the value must not be persisted.</remarks>
        public int GetHashCode([DisallowNull] Exception obj)
            => obj?.StackTrace?.GetHashCode() ?? 0;
    }




    /// <summary>
    /// Extension methods for exceptions
    /// </summary>
    /// <remarks>
    /// <see cref="SafeMessage"/> and <see cref="SafeText"/> (in ExceptionExt.Safe.cs) returns exception texts with sensitive information removed, use them for texts that are sent to clients.
    /// </remarks>
    public static partial class ExceptionExt
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
        /// <param name="ex">The exception to set a new message text on, must not be null</param>
        /// <param name="newException">The new text (if null, <see cref="Exception.Message"/> returns a default type based message)</param>
        /// <exception cref="NullReferenceException"><paramref name="ex"/> is null</exception>
        /// <exception cref="TypeInitializationException">The private <c>_message</c> field of <see cref="Exception"/> can't be found in the current runtime</exception>
        /// <remarks>
        /// Exception types that override <see cref="Exception.Message"/> may not return the new text.
        /// The setter is a compiled expression (created once in the static constructor), so calls are fast.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetMessage(this Exception ex, String newException)
            => InternalSetText(ex, newException);
    }


}
