using System.Collections.Generic;

namespace SysWeaver
{
    public sealed class ArrayEqualityComparer
    {
        struct ComparerT<T> : IEqualityComparer<T[]>
        {

            public ComparerT(IEqualityComparer<T> comp)
            {
                Comp = comp;
            }

            readonly IEqualityComparer<T> Comp;

            public bool Equals(T[] x, T[] y)
            {
                if (x == null)
                    return y == null;
                if (y == null)
                    return false;
                var l = x.Length;
                if (y.Length != l)
                    return false;
                var cmp = Comp;
                for (int i = 0; i < l; ++i)
                {
                    var xx = x[i];
                    var yy = y[i];
                    if (!cmp.Equals(xx, yy))
                        return false;
                }
                return true;
            }

            public int GetHashCode(T[] obj)
            {
                if (obj == null)
                    return 0;
                var l = obj.Length;
                var h = l + 1;
                var cmp = Comp;
                for (int i = 0; i < l; ++i)
                {
                    h *= 70001;
                    var o = obj[i];
                    h += cmp.GetHashCode(o);
                }
                return h;
            }

            public static readonly IEqualityComparer<T[]> Def = new ComparerT<T>(EqualityComparer<T>.Default);
        }

        /// <summary>
        /// Get an array comparer, that uses the default element comparer
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static IEqualityComparer<T[]> Get<T>()
            => ComparerT<T>.Def;


        /// <summary>
        /// Get an array comparer, with the specified element comparer
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="comparer">Element comparer</param>
        /// <returns></returns>
        public static IEqualityComparer<T[]> Get<T>(IEqualityComparer<T> comparer)
            => comparer == null ? ComparerT<T>.Def : new ComparerT<T>(comparer);

    }

}
