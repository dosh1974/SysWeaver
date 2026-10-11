
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

        /// <summary>
        /// Rank the similarity of the texts to the search text, using an explicit weight per text
        /// (the score of each text is multiplied by its weight, no implicit weight decay by position is applied).
        /// The default implementation (for rankers that don't support weights) ignores the weights and returns <see cref="Rank(string[])"/>,
        /// so the texts should be passed in order of importance.
        /// </summary>
        /// <param name="weights">The weight of each text (same index as <paramref name="texts"/>), a weight aware ranker ignores texts with a weight of zero or less</param>
        /// <param name="texts">The texts to rank (null or empty texts are ignored), may not contain more texts than <paramref name="weights"/></param>
        /// <returns>A score, higher is a better match, zero (or less) means no match.
        /// The scale is implementation specific, so scores should only be compared to other scores from the same ranker.</returns>
        double Rank(double[] weights, params String[] texts) => Rank(texts);
    }




}
