
using System;
using System.Collections.Generic;

namespace SysWeaver.Search
{
    /// <summary>
    /// Interface to a text search implementation (a factory for searchers and rankers)
    /// </summary>
    /// <remarks>
    /// Used by the table data system to rank rows when doing a free text search, <see cref="SimpleTextSearch"/> is the default implementation.
    /// </remarks>
    public interface ITextSearch
    {
        /// <summary>
        /// Create a searcher, suitable for static or semi-static data
        /// </summary>
        /// <typeparam name="T">The type of content associated with the texts</typeparam>
        /// <param name="comparer">The comparer used to identify content, null to use the default comparer</param>
        /// <returns>A new empty searcher</returns>
        ITextSearcher<T> CreateSearcher<T>(IEqualityComparer<T> comparer = null);

        /// <summary>
        /// Create a ranker, suitable for dynamic data
        /// </summary>
        /// <param name="searchText">The text to search for</param>
        /// <returns>A ranker that ranks texts against <paramref name="searchText"/></returns>
        ITextRanker CreateRanker(String searchText);

    }




}
