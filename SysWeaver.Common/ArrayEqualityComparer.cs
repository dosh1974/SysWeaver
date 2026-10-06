using System;
using System.Collections.Generic;

namespace SysWeaver
{
    /// <summary>
    /// Creates equality comparers for arrays, two arrays are equal if they have the same length and all elements are equal (using an element comparer).
    /// </summary>
    /// <remarks>
    /// Two null arrays are equal, a null array is never equal to a non-null array (not even an empty one).
    /// Null elements are only equal to other null elements, the element comparer is never called with a null element.
    /// The hash code of a null array is 0, null elements doesn't contribute to the hash code (but the element position does).
    /// </remarks>
    public sealed class ArrayEqualityComparer
    {
        /// <summary>
        /// The array comparer implementation (a struct, boxed once when returned as an interface).
        /// </summary>
        struct ComparerT<T> : IEqualityComparer<T[]>
        {

            public ComparerT(IEqualityComparer<T> comp)
            {
                Comp = comp;
                IsDefault = ReferenceEquals(comp, EqualityComparer<T>.Default);
            }

            readonly IEqualityComparer<T> Comp;

            /// <summary>
            /// True if the element comparer is the default comparer (enables faster code paths)
            /// </summary>
            readonly bool IsDefault;

            public bool Equals(T[] x, T[] y)
            {
                if (x == null)
                    return y == null;
                if (y == null)
                    return false;
                var l = x.Length;
                if (y.Length != l)
                    return false;
                // The default comparer handles nulls the same way as below, use the (vectorized for bitwise equatable types) span comparison
                if (IsDefault)
                    return new ReadOnlySpan<T>(x).SequenceEqual(new ReadOnlySpan<T>(y), null);
                var cmp = Comp;
                for (int i = 0; i < l; ++i)
                {
                    var xx = x[i];
                    var yy = y[i];
                    if (xx == null)
                    {
                        if (yy != null)
                            return false;
                        continue;
                    }
                    if (yy == null)
                        return false;
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
                    if (o != null)
                        h += cmp.GetHashCode(o);
                }
                return h;
            }

            /// <summary>
            /// The cached comparer that uses <see cref="EqualityComparer{T}.Default"/> for the elements
            /// </summary>
            public static readonly IEqualityComparer<T[]> Def = new ComparerT<T>(EqualityComparer<T>.Default);
        }

        /// <summary>
        /// Get an array comparer, that uses the default element comparer (<see cref="EqualityComparer{T}.Default"/>).
        /// The same (cached) instance is returned for every call with the same <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The array element type</typeparam>
        /// <returns>An equality comparer for arrays of <typeparamref name="T"/></returns>
        public static IEqualityComparer<T[]> Get<T>()
            => ComparerT<T>.Def;


        /// <summary>
        /// Get an array comparer, with the specified element comparer
        /// </summary>
        /// <typeparam name="T">The array element type</typeparam>
        /// <param name="comparer">Element comparer, if null the default element comparer is used (and the cached instance is returned, same as <see cref="Get{T}()"/>)</param>
        /// <returns>An equality comparer for arrays of <typeparamref name="T"/></returns>
        /// <remarks>A new comparer instance is allocated for every call with a non-null <paramref name="comparer"/>, cache it if it's used frequently.</remarks>
        public static IEqualityComparer<T[]> Get<T>(IEqualityComparer<T> comparer)
            => comparer == null ? ComparerT<T>.Def : new ComparerT<T>(comparer);

    }

}
