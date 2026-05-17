
using System;

namespace SysWeaver.Search
{
    /// <summary>
    /// Interface to a similarity ranker
    /// </summary>
    public interface ITextRanker
    {
        /// <summary>
        /// Rank the similarity of the texts 
        /// </summary>
        /// <param name="texts"></param>
        /// <returns></returns>
        double Rank(params String[] texts);
    }




}
