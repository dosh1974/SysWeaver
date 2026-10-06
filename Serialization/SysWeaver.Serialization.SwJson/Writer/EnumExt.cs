using System.Collections;
using System.Collections.Generic;

namespace SysWeaver.Serialization.SwJson.Writer
{
    /// <summary>
    /// Enumerable helpers used when building the writers
    /// </summary>
    static class EnumExt
    {
        /// <summary>
        /// Return an <see cref="IEnumerable{T}"/> with this value as the only item
        /// </summary>
        /// <remarks>
        /// Allocates (the enumerable is boxed and the enumerator is an iterator), only used when building writers (not on the hot path).
        /// </remarks>
        /// <typeparam name="T">The type of the value</typeparam>
        /// <param name="value">The value</param>
        /// <returns>An enumerable that yields <paramref name="value"/> once</returns>
        public static IEnumerable<T> AsEnumerable<T>(this T value) => new SingleEnum<T>(value);

        /// <summary>
        /// An enumerable with a single item
        /// </summary>
        struct SingleEnum<T> : IEnumerable<T>
        {
            public SingleEnum(T value)
            {
                V = value;
            }
            readonly T V;
            public IEnumerator<T> GetEnumerator()
            {
                yield return V;
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        }
    }

}
