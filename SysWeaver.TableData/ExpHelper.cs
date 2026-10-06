using System.Linq.Expressions;

namespace SysWeaver.Data
{
    /// <summary>
    /// Cached constant expressions for a type, used when building expression trees.
    /// </summary>
    /// <typeparam name="T">The type of the constant.</typeparam>
    /// <remarks>
    /// Only use with reference types or <see cref="System.Nullable{T}"/>: <see cref="Null"/> can't be created for a non-nullable value type,
    /// making the type initializer (and therefore both fields) fail with a <see cref="System.TypeInitializationException"/>.
    /// </remarks>
    public static class ExpHelper<T>
    {
        /// <summary>
        /// A constant expression with the value <c>default(T)</c>, typed as <typeparamref name="T"/>.
        /// </summary>
        public static readonly ConstantExpression Default = Expression.Constant(default(T), typeof(T));

        /// <summary>
        /// A constant null expression typed as <typeparamref name="T"/>.
        /// </summary>
        public static readonly ConstantExpression Null = Expression.Constant(null, typeof(T));
    }








}
