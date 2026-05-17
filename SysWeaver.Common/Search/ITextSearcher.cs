
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SysWeaver.Search
{



    public interface ITextSearcher<T> : IEnumerable<T>
    {
        /// <summary>
        /// Add texts and the associated content to the searcher
        /// </summary>
        /// <param name="content"></param>
        /// <param name="texts"></param>
        /// <returns></returns>
        ValueTask<bool> TryAdd(T content, params String[] texts);

        /// <summary>
        /// Remove content from the searcher
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        ValueTask<bool> TryRemove(T content);

        /// <summary>
        /// Search for some text
        /// </summary>
        /// <param name="text"></param>
        /// <param name="maxHits"></param>
        /// <param name="keepResult"></param>
        /// <returns></returns>
        ValueTask<ValueTuple<T, double>[]> Search(String text, int maxHits = 10, Func<T, ValueTask<bool>> keepResult = null);

    }




}
