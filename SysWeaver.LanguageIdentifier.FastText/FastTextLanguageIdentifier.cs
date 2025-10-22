using FastText.NetWrapper;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Compression;
using SysWeaver.IsoData;

namespace SysWeaver.LanguageIdentifier
{

    /// <summary>
    /// Language identification using Facebook's FastText.
    /// Very fast but not so good, very bad at short sentences.
    /// Embeds and uses by default the compressed model: lid.176.ftz.
    /// A better (much bigger) model can be downloaded and suppplied in the parameters here:
    /// https://fasttext.cc/docs/en/language-identification.html
    /// </summary>
    public sealed class FastTextLanguageIdentifier : ILanguageIdentifier, IDisposable, IPerfMonitored, IHaveStats
    {
        public FastTextLanguageIdentifier(FastTextLanguageIdentifierParams p = null)
        {
            p = p ?? new FastTextLanguageIdentifierParams();
            MaxChecked = Math.Max(1, p.MaxChecked);
            var ts = TimeSpan.FromSeconds(Math.Max(5, p.CacheSeconds));
            Cache = new FastMemCache<string, ValueTask<string>>(ts, StringComparer.Ordinal);
            CacheList = new FastMemCache<string, ValueTask<IdentifiedLanguage[]>>(ts, StringComparer.Ordinal);
            var model = p.ModelFile;
            var ft = new FastTextWrapper();
            FT = ft;
            if (string.IsNullOrEmpty(model))
            {
                //var names = typeof(FastTextLanguageIdentifier).Assembly.GetManifestResourceNames();
                var data = typeof(FastTextLanguageIdentifier).GetUncompressedResourceDataBytes("SysWeaver.LanguageIdentifier.data.lid.176.ftz");
                ft.LoadModel(data);
            }
            else
            {
                model = PathTemplate.Resolve(model);
                ft.LoadModel(model);
            }
            if (!ft.IsModelReady())
                throw new Exception("Model is not ready!");
        }

        readonly int MaxChecked;
        readonly FastMemCache<String, ValueTask<string>> Cache;
        readonly FastMemCache<String, ValueTask<IdentifiedLanguage[]>> CacheList;

        public void Dispose()
        {
            FT.Dispose();
        }

        readonly FastTextWrapper FT;

        readonly ExceptionTracker Errors = new ExceptionTracker();

        public ValueTask<string> Identify(string text, string userLanguge = null, double userLanguageBias = 0.2, double minConfidence = 0.05)
            => Cache.GetOrUpdate(String.Join("\n\r\t", text, userLanguge, userLanguageBias, minConfidence), f =>
            {
                using var __ = PerfMon.Track(nameof(Identify));
                try
                {
                    Prediction res;
                    if (userLanguge == null)
                    {
                        res = FT.PredictSingle(text);
                    }
                    else
                    {
                        var t = FT.PredictMultiple(text, MaxChecked);
                        res = t[0];
                        if (!res.Label.FastEndsWith(userLanguge))
                        {
                            var tl = t.Length;
                            for (int i = 1; i < tl; ++i)
                            {
                                var c = t[i];
                                if (!c.Label.FastEndsWith(userLanguge))
                                    continue;
                                var newScore = c.Probability + userLanguageBias;
                                if (newScore >= res.Probability)
                                {
                                    minConfidence -= userLanguageBias;
                                    res = c;
                                }
                                break;
                            }
                        }
                    }
                    if (res.Probability < minConfidence)
                        return TaskExt.NullStringValueTask;
                    var lang = IsoLanguage.TryGet(res.Label.Substring(9))?.Iso639_1;
                    return ValueTask.FromResult(lang);
                }
                catch (Exception ex)
                {
                    Errors.OnException(ex);
                    return TaskExt.NullStringValueTask;
                }
            });


        static readonly ValueTask<IdentifiedLanguage[]> NoTask = ValueTask.FromResult<IdentifiedLanguage[]>(null);


        public ValueTask<IdentifiedLanguage[]> Identify(String text, int numberOfResults, String userLanguge = null, double userLanguageBias = 0.2)
            => CacheList.GetOrUpdate(String.Join("\n\r\t", text, numberOfResults, userLanguge, userLanguageBias), f =>
            {
                using var __ = PerfMon.Track(nameof(Identify));
                try
                {
                    var t = FT.PredictMultiple(text, numberOfResults).Select(x => new IdentifiedLanguage(IsoLanguage.TryGet(x.Label.Substring(9))?.Iso639_1, x.Probability)).Where(x => x.Language != null).ToList();
                    if (t == null)
                        return NoTask;
                    if (userLanguge != null)
                    { 
                        if (!t[0].Language.FastEquals(userLanguge))
                        {
                            var tl = t.Count;
                            for (int i = 1; i < tl; ++i)
                            {
                                var c = t[i];
                                if (!c.Language.FastEquals(userLanguge))
                                    continue;
                                c.Confidence += userLanguageBias;
                                t.Sort((a, b) => b.Confidence.CompareTo(a.Confidence));
                                break;
                            }
                        }
                    }
                    return ValueTask.FromResult(t.ToArray());
                }
                catch (Exception ex)
                {
                    Errors.OnException(ex);
                    return NoTask;
                }
            });

        public IEnumerable<Stats> GetStats()
        {
            foreach (var x in Cache.GetStats(nameof(FastTextLanguageIdentifier), "Simple."))
                yield return x;
            foreach (var x in CacheList.GetStats(nameof(FastTextLanguageIdentifier), "Array."))
                yield return x;
            foreach (var x in Errors.GetStats(nameof(FastTextLanguageIdentifier), "Fails."))
                yield return x;
        }

        public PerfMonitor PerfMon { get; } = new PerfMonitor(nameof(FastTextLanguageIdentifier));

    }
}
