
using System;
using System.Collections.Generic;

namespace SysWeaver.Search
{

    /// <summary>
    /// A simple text search implementation, texts are matched by finding the lower cased words and numbers of the search text as sub strings.
    /// </summary>
    /// <remarks>
    /// Searchers are thread safe, but searching is a linear scan of all content (no index), so it's only suitable for small to medium amounts of data.
    /// This is the default search used by the table data system.
    /// </remarks>
    public sealed partial class SimpleTextSearch : ITextSearch
    {

        /// <inheritdoc/>
        /// <remarks>Texts are lower cased when added and empty texts are ignored.</remarks>
        public ITextSearcher<T> CreateSearcher<T>(IEqualityComparer<T> comparer = null) => new Searcher<T>(comparer);

        /// <inheritdoc/>
        /// <remarks>The search text is split into words and numbers, a text with none of them never matches.</remarks>
        public ITextRanker CreateRanker(String searchText) => new Ranker(searchText);


    }

}
