using System;
using System.Linq.Expressions;

namespace SysWeaver
{
    public static class LinkTools
    {
        public static Expression<Func<R>> Func<R>(Expression<Func<R>> value) => value;
        
        public static Expression<Func<A0, R>> Func<A0, R>(Expression<Func<A0, R>> value) => value;

        public static Expression<Func<A0, A1, R>> Func<A0, A1, R>(Expression<Func<A0, A1, R>> value) => value;

    }

}
