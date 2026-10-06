using System;
using System.Collections.Generic;

namespace SysWeaver
{
    /// <summary>
    /// Factory methods for <see cref="StringTreeList{T}"/> (allows the type of the values to be inferred)
    /// </summary>
    public static class StringTreeList
    {
        /// <summary>
        /// Build a tree from a bunch of strings with values, see <see cref="StringTreeList{T}.Build(IEnumerable{Tuple{string, T}}, bool)"/>
        /// </summary>
        /// <typeparam name="T">The type of the values</typeparam>
        /// <param name="strings">The strings (and values) to build a tree from, the strings may not be null or empty</param>
        /// <param name="caseInSensitive">Set to true to make a case in-sensitive tree</param>
        /// <returns>The tree</returns>
        public static StringTreeList<T> Build<T>(IEnumerable<Tuple<String, T>> strings, bool caseInSensitive = false)
            => StringTreeList<T>.Build(strings, caseInSensitive);

        /// <summary>
        /// Build a tree from a bunch of strings with values, see <see cref="StringTreeList{T}.Build(IEnumerable{KeyValuePair{string, T}}, bool)"/>
        /// </summary>
        /// <typeparam name="T">The type of the values</typeparam>
        /// <param name="strings">The strings (and values) to build a tree from, the strings may not be null or empty</param>
        /// <param name="caseInSensitive">Set to true to make a case in-sensitive tree</param>
        /// <returns>The tree</returns>
        public static StringTreeList<T> Build<T>(IEnumerable<KeyValuePair<String, T>> strings, bool caseInSensitive = false)
            => StringTreeList<T>.Build(strings, caseInSensitive);
        /// <summary>
        /// Build a tree from a bunch of values, see <see cref="StringTreeList{T}.Build(IEnumerable{T}, Func{T, string}, bool)"/>
        /// </summary>
        /// <typeparam name="T">The type of the values</typeparam>
        /// <param name="values">The values to add</param>
        /// <param name="getKey">Function that extracts the string key (may not return null or an empty string)</param>
        /// <param name="caseInSensitive">Set to true to make a case in-sensitive tree</param>
        /// <returns>The tree</returns>
        public static StringTreeList<T> Build<T>(IEnumerable<T> values, Func<T, String> getKey, bool caseInSensitive = false)
            => StringTreeList<T>.Build(values, getKey, caseInSensitive);
    }



}
