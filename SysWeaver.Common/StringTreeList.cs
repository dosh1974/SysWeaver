using System;
using System.Collections.Generic;

namespace SysWeaver
{
    public static class StringTreeList
    {
        /// <summary>
        /// Build a tree from a bunch of strings
        /// </summary>
        /// <param name="strings">The strings to build a tree from, may not contain null</param>
        /// <param name="caseInSensitive">Set to true to make a case in-sensitive tree</param>
        /// <returns>The tree</returns>
        public static StringTreeList<T> Build<T>(IEnumerable<Tuple<String, T>> strings, bool caseInSensitive = false)
            => StringTreeList<T>.Build(strings, caseInSensitive);

        /// <summary>
        /// Build a tree from a bunch of strings
        /// </summary>
        /// <param name="strings">The strings to build a tree from, may not contain null</param>
        /// <param name="caseInSensitive">Set to true to make a case in-sensitive tree</param>
        /// <returns>The tree</returns>
        public static StringTreeList<T> Build<T>(IEnumerable<KeyValuePair<String, T>> strings, bool caseInSensitive = false)
            => StringTreeList<T>.Build(strings, caseInSensitive);
        /// <summary>
        /// Build a tree from a bunch of strings
        /// </summary>
        /// <param name="values">The values to add, may not contain null</param>
        /// <param name="getKey">Function that extracts the string key</param>
        /// <param name="caseInSensitive">Set to true to make a case in-sensitive tree</param>
        /// <returns>The tree</returns>
        public static StringTreeList<T> Build<T>(IEnumerable<T> values, Func<T, String> getKey, bool caseInSensitive = false)
            => StringTreeList<T>.Build(values, getKey, caseInSensitive);
    }



}
