
using System;
using System.Collections.Generic;

namespace SysWeaver.Search
{
    /// <summary>
    /// Interface to a text searcher
    /// </summary>
    public interface ITextSearch
    {
        /// <summary>
        /// Create a searcher, suitable for static or semi-static data
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        ITextSearcher<T> CreateSearcher<T>(IEqualityComparer<T> comparer = null);

        /// <summary>
        /// Create a ranker, suitable for dynamic data
        /// </summary>
        /// <param name="searchText"></param>
        /// <returns></returns>
        ITextRanker CreateRanker(String searchText);

    }




}
