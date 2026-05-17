
using System;
using System.Collections.Generic;

namespace SysWeaver.Search
{

    public sealed partial class SimpleTextSearch : ITextSearch
    {

        public ITextSearcher<T> CreateSearcher<T>(IEqualityComparer<T> comparer = null) => new Searcher<T>(comparer);

        public ITextRanker CreateRanker(String searchText) => new Ranker(searchText);


    }

}
