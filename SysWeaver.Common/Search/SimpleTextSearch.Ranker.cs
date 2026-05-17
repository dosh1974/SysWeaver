
using System;
using System.Linq;
using System.Buffers;

namespace SysWeaver.Search
{

    public sealed partial class SimpleTextSearch
    {
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
                var search = W;
                double textWeight = 1.0;
                var tl = texts.Length;
                var sl = search.Length;
                double score = 0;
                for (int t = 0; t < tl; ++t)
                {
                    var tt = texts[t].FastToLower();
                    if (String.IsNullOrEmpty(tt))
                        continue;
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
                    textWeight *= 0.95;
                }
                return score;
            }
            public readonly String[] W;

        }


    }

}
