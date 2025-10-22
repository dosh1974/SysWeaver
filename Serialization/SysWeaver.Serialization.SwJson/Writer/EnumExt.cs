using System.Collections;
using System.Collections.Generic;

namespace SysWeaver.Serialization.SwJson.Writer
{
    static class EnumExt
    {
        /// <summary>
        /// Return an IEnuerable with this value in it
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <returns></returns>
        public static IEnumerable<T> AsEnumerable<T>(this T value) => new SingleEnum<T>(value);

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
