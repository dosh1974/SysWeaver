using System.Linq.Expressions;

namespace SysWeaver.Data
{
    public static class ExpHelper<T>
    {
        public static readonly ConstantExpression Default = Expression.Constant(default(T), typeof(T));
        public static readonly ConstantExpression Null = Expression.Constant(null, typeof(T));
    }








}
