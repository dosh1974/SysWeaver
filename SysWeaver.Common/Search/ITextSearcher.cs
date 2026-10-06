
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SysWeaver.Search
{



    /// <summary>
    /// A searchable collection of content, each with one or more associated texts.
    /// Enumerating the searcher returns the content (in no particular order).
    /// </summary>
    /// <typeparam name="T">The type of content associated with the texts</typeparam>
    public interface ITextSearcher<T> : IEnumerable<T>
    {
        /// <summary>
        /// Add texts and the associated content to the searcher
        /// </summary>
        /// <param name="content">The content to add (must be unique)</param>
        /// <param name="texts">The texts to search (in order of importance)</param>
        /// <returns>True if the content was added, false if it already exists or no (non-empty) texts were supplied</returns>
        Task<bool> TryAdd(T content, params String[] texts);

        /// <summary>
        /// Remove content from the searcher
        /// </summary>
        /// <param name="content">The content to remove</param>
        /// <returns>True if the content was removed, false if it wasn't found</returns>
        Task<bool> TryRemove(T content);

        /// <summary>
        /// Search for some text
        /// </summary>
        /// <param name="text">The text to search for</param>
        /// <param name="maxHits">The maximum number of results to return</param>
        /// <param name="keepResult">An optional filter, only content where this returns true is included</param>
        /// <returns>The matching content and their scores, best match first</returns>
        Task<ValueTuple<T, double>[]> Search(String text, int maxHits = 10, Func<T, Task<bool>> keepResult = null);

    }




}
