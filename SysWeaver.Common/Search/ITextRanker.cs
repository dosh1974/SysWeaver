
using System;

namespace SysWeaver.Search
{
    /// <summary>
    /// Interface to a similarity ranker, ranks texts against the search text that the ranker was created with (see <see cref="ITextSearch.CreateRanker"/>)
    /// </summary>
    public interface ITextRanker
    {
        /// <summary>
        /// Rank the similarity of the texts to the search text
        /// </summary>
        /// <param name="texts">The texts to rank (in order of importance, null or empty texts are ignored)</param>
        /// <returns>A score, higher is a better match, zero (or less) means no match.
        /// The scale is implementation specific, so scores should only be compared to other scores from the same ranker.</returns>
        double Rank(params String[] texts);
    }




}
