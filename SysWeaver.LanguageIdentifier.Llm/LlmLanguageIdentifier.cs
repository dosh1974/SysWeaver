using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using SysWeaver.AI;
using SysWeaver.IsoData;
using SysWeaver.MicroService;

namespace SysWeaver.LanguageIdentifier
{

    [IsMicroService]
    [RequiredDep<OpenAiService>]
    public sealed class LlmLanguageIdentifier : ILanguageIdentifier, IDisposable, IPerfMonitored, IHaveStats
    {
        public LlmLanguageIdentifier(ServiceManager manager, LlmLanguageIdentifierParams p = null)
        {
            p = p ?? new LlmLanguageIdentifierParams();
            var ts = TimeSpan.FromSeconds(Math.Max(5, p.CacheSeconds));
            ConLock = new AsyncLock(Math.Max(1, p.MaxConcurrency));
            Cache = new FastMemCache<string, string>(ts, StringComparer.Ordinal);
            CacheList = new FastMemCache<string, IdentifiedLanguage[]>(ts, StringComparer.Ordinal);
            Ai = manager.Get<OpenAiService>();
            Model = p.Model;
        }


        readonly AsyncLock ConLock;
        readonly ConcurrentStack<OpenAiQuerySession> Sessions = new ConcurrentStack<OpenAiQuerySession>();
        readonly OpenAiService Ai;
        readonly string Model;
        readonly FastMemCache<String, string> Cache;
        readonly FastMemCache<String, IdentifiedLanguage[]> CacheList;


        public void Dispose()
        {
        }

        const String TaskDesc =
                            """
                            Your job is to identify the language used by the text in the user message.
                            If the text is present in multiple languages, prefer the most widely spoken a bit more.
                            It's very important that you properly identify the language.
                            Put a lot of effort into it.
                            
                            """;

        const String SingleNoLang =
                            TaskDesc + 
                            """
                            Output a two letter ISO 639-1 language code of the identified language and a confidence score between zero and one.
                            Example: "sv 0.45".

                            Never output anything else.
                            
                            """;


        const String MultipleNoLang =
                            TaskDesc +
                            """
                            Output a list of up to {3} two letter ISO 639-1 language code of the identified language and a confidence score between zero and one.
                            Each score on a new line.
                            Example: 
                            "sv 0.45".
                            "da 0.35".
                            "no 0.25".
                            
                            Order the output according to confidence in descending order.
                            Never output anything else.

                            """;


        const String LangPrefix =
                            """

                            The language selected by the user when entering the text was {0} (ISO {1}), boost the confidence of that language with {2}%.
                            """;

        const String SingleWithLang = SingleNoLang + LangPrefix;

        const String MultipleWithLang = MultipleNoLang + LangPrefix;

        readonly ExceptionTracker Errors = new ExceptionTracker();

        public ValueTask<string> Identify(string text, string userLanguge = null, double userLanguageBias = 0.2, double minConfidence = 0.05)
            => Cache.GetOrUpdateValueAsync(String.Join("\n\r\t", text, userLanguge, userLanguageBias, minConfidence), async f =>
            {
                using var _ = await ConLock.Lock().ConfigureAwait(false);
                using var __ = PerfMon.Track(nameof(Identify));
                var sessions = Sessions;
                if (!sessions.TryPop(out var session))
                {
                    session = Ai.CreateQuerySession(Model);
                    session.Temperature = 0;
                }
                try
                {
                    session.SystemPrompt = String.Format(userLanguge == null ? SingleNoLang : SingleWithLang, IsoLanguage.TryGetName(userLanguge)?.Name, userLanguge, (int)(userLanguageBias * 200));
                    var res = (await session.Query(text).ConfigureAwait(false))?.Trim();
                        if (String.IsNullOrEmpty(res))
                            return null;
                        return IsoLanguage.TryGetName(res.Trim(Trims).SplitFirst(' ').Trim(Trims))?.Iso639_1;
                }
                catch (Exception ex)
                {
                    Errors.OnException(ex);
                    return null;
                }
                finally
                {
                    sessions.Push(session);
                }
            });


        static readonly Char[] Trims = "\r\n\t\'\". ,-!*+".ToCharArray();


        public ValueTask<IdentifiedLanguage[]> Identify(string text, int numberOfResults, string userLanguge = null, double userLanguageBias = 0.2)
            => CacheList.GetOrUpdateValueAsync(String.Join("\n\r\t", text, userLanguge, userLanguageBias, numberOfResults), async f =>
            {
                using var _ = await ConLock.Lock().ConfigureAwait(false);
                using var __ = PerfMon.Track(nameof(Identify));
                var sessions = Sessions;
                if (!sessions.TryPop(out var session))
                {
                    session = Ai.CreateQuerySession(Model);
                    session.Temperature = 0;
                }
                try
                {
                    session.SystemPrompt = String.Format(userLanguge == null ? MultipleNoLang : MultipleWithLang, IsoLanguage.TryGetName(userLanguge)?.Name, userLanguge, (int)(userLanguageBias * 200), numberOfResults);
                    var res = await session.Query(text).ConfigureAwait(false);
                    if (String.IsNullOrEmpty(res))
                        return null;
                    var lines = res.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    var languages = lines.Select(x =>
                    {
                        var t = x.Trim(Trims).SplitFirst(' ', out var val).Trim(Trims);
                        var l = IsoLanguage.TryGetName(t)?.Iso639_1;
                        if (l == null)
                            return null;
                        if (!double.TryParse(val.TrimStart(), CultureInfo.InvariantCulture, out var conf))
                            return null;
                        return new IdentifiedLanguage(l, conf);
                    }).Where(x => x != null).ToArray();
                    return languages;
                }
                catch (Exception ex)
                {
                    Errors.OnException(ex);
                    return null;
                }
                finally
                {
                    sessions.Push(session);
                }
            });

        public IEnumerable<Stats> GetStats()
        {
            foreach (var x in Cache.GetStats(nameof(LlmLanguageIdentifier), "Simple."))
                yield return x;
            foreach (var x in CacheList.GetStats(nameof(LlmLanguageIdentifier), "Array."))
                yield return x;
            foreach (var x in Errors.GetStats(nameof(LlmLanguageIdentifier), "Fails."))
                yield return x;
        }

        public PerfMonitor PerfMon { get; } = new PerfMonitor(nameof(LlmLanguageIdentifier));

    }


}
