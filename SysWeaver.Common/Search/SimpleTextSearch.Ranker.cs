
using System;
using System.Linq;
using System.Buffers;

namespace SysWeaver.Search
{

    public sealed partial class SimpleTextSearch
    {
        /// <summary>
        /// Ranks texts by finding the (lower case) words and numbers of the search text in them.
        /// A match scores higher if it's early in the text, starts or ends at a word boundary and comes after the previous match.
        /// Each subsequent (non empty) text has 0.95 times the weight of the previous text, unless explicit weights are passed to <see cref="Rank(double[], string[])"/>.
        /// </summary>
        struct Ranker : ITextRanker
        {
            public Ranker(String text)
            {
                var searchWords = text.ExtractWordsAndNumbers().ToArray();
                var sl = searchWords.Length;
                for (int i = 0; i < sl; ++i)
                    searchWords[i] = searchWords[i].FastToLower();
                W = searchWords;
            }

            public double Rank(params String[] texts)
            {
                double textWeight = 1.0;
                var tl = texts.Length;
                double score = 0;
                for (int t = 0; t < tl; ++t)
                {
                    var tt = texts[t];
                    if (String.IsNullOrEmpty(tt))
                        continue;
                    RankText(ref score, tt, textWeight);
                    textWeight *= 0.95;
                }
                return score;
            }

            /// <summary>
            /// Rank with an explicit weight per text (no decay by position), texts with a weight of zero or less and null or empty texts are ignored.
            /// </summary>
            public double Rank(double[] weights, params String[] texts)
            {
                var tl = texts.Length;
                double score = 0;
                for (int t = 0; t < tl; ++t)
                {
                    var w = weights[t];
                    if (w <= 0)
                        continue;
                    var tt = texts[t];
                    if (String.IsNullOrEmpty(tt))
                        continue;
                    RankText(ref score, tt, w);
                }
                return score;
            }

            readonly void RankText(ref double score, String tt, double textWeight)
            {
                var search = W;
                var sl = search.Length;
                tt = tt.FastToLower();
                var text = tt.AsSpan();
                var ttl = text.Length;
                int prev = 0;
                for (int s = 0; s < sl; ++s)
                {
                    var st = search[s].AsSpan();
                    var pos = text.IndexOf(st);
                    if (pos < 0)
                        continue;
                    double ws = 1.0;
                    if ((pos == 0) || (!Char.IsLetterOrDigit(text[pos - 1])))
                        ws += 1.0;
                    var lp = pos + st.Length;
                    if ((lp >= ttl) || (!Char.IsLetterOrDigit(text[lp])))
                        ws += 0.5;
                    double posScore = 1.5 / (1.0 + pos);
                    var dp = pos - prev;
                    double prevScore = dp < 0 ? 0.01 : (1.0 / (1.0 + dp));
                    prev = lp + 1;
                    score += (posScore + prevScore) * textWeight * ws;
                }
            }
            public readonly String[] W;

        }


    }

}
