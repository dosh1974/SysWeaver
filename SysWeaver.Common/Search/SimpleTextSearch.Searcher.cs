
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections;

namespace SysWeaver.Search
{

    public sealed partial class SimpleTextSearch
    {
        sealed class Searcher<T> : ITextSearcher<T>
        {

            public Searcher(IEqualityComparer<T> comparer = null)
            {
                Texts = comparer == null ? new ConcurrentDictionary<T, string[]>() : new ConcurrentDictionary<T, string[]>(comparer);
            }

            readonly ConcurrentDictionary<T, String[]> Texts;


            public Task<bool> TryAdd(T content, params String[] texts)
            {
                texts = texts.Where(x => !String.IsNullOrEmpty(x)).Select(x => x.FastToLower()).ToArray();
                if (texts.Length <= 0)
                    return TaskExt.FalseTask;
                return Texts.TryAdd(content, texts) ? TaskExt.TrueTask : TaskExt.FalseTask;
            }

            public Task<bool> TryRemove(T content)
                => Texts.TryRemove(content, out var _) ? TaskExt.TrueTask : TaskExt.FalseTask;

            public async Task<ValueTuple<T, double>[]> Search(String text, int maxHits = 10, Func<T, Task<bool>> keepResult = null)
            {
                if (maxHits < 1)
                    maxHits = 1;
                var r = new Ranker(text);
                PriorityQueue<T, double> results = new(maxHits + 1);
                if (keepResult == null)
                {
                    foreach (var x in Texts)
                    {
                        var score = r.Rank(x.Value);
                        if (score <= 0.0)
                            continue;
                        if (results.Count < maxHits)
                            results.Enqueue(x.Key, score);
                        else
                            results.EnqueueDequeue(x.Key, score);
                    }
                }
                else
                {
                    foreach (var x in Texts)
                    {
                        var i = x.Key;
                        if (!await keepResult(i).ConfigureAwait(false))
                            continue;
                        var score = r.Rank(x.Value);
                        if (score <= 0.0)
                            continue;
                        if (results.Count < maxHits)
                            results.Enqueue(i, score);
                        else
                            results.EnqueueDequeue(i, score);
                    }

                }
                var t = results.UnorderedItems.Select(x => ValueTuple.Create(x.Element, x.Priority)).ToArray();
                t.Sort((a, b) => b.Item2.CompareTo(a.Item2));
                return t;
            }

            public IEnumerator<T> GetEnumerator() => Texts.Keys.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => Texts.Keys.GetEnumerator();


        }


    }

}
