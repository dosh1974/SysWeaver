using OpenAI.Chat;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.IsoData;
using SysWeaver.MicroService;
using SysWeaver.Translation;

namespace SysWeaver.AI
{

    public sealed class LlmTranslator : IInternalTranslator, IPerfMonitored, IHaveStats
    {

        public const String MD = "{MD}";

        public LlmTranslator(ServiceManager manager, LlmTranslatorParams p = null)
        {
            p = p ?? new LlmTranslatorParams();
            RetryCount = Math.Max(0, p.RetryCount);
            Llm = manager.Get<OpenAiService>(p.AiInstance);
            CountTokensTasks[0] = CountTokens0;
            CountTokensTasks[1] = CountTokens1;
            CountTokensTasks[2] = CountTokens2;
            Models[0] = p.LowModel;
            Models[1] = p.MediumModel;
            Models[2] = p.HighModel;
            if (p.ForceLanguagesOnLoad)
            {
                manager.AddMessage("Fetching supported languages");
                TryGetLanguagesNow(true).RunAsync();
                manager.AddMessage("Fetched supported languages");

            }
            else
            {
                if (p.GetLanguagesOnLoad)
                {
                    manager.AddMessage("Loading/Fetching supported languages");
                    TryGetLanguagesNow().RunAsync();
                    manager.AddMessage("Loaded/Fetched supported languages");

                }
            }
        }

        readonly OpenAiService Llm;
        readonly String[] Models = new string[3];



        public PerfMonitor PerfMon { get; } = new PerfMonitor(nameof(LlmTranslator));



        #region Session cache

        readonly ConcurrentStack<QuerySession>[] Sessions =
            [
                new ConcurrentStack<QuerySession>(),
                new ConcurrentStack<QuerySession>(),
                new ConcurrentStack<QuerySession>()
            ];

        sealed class QuerySession : OpenAiQuerySession, IDisposable
        {
            public QuerySession(ConcurrentStack<QuerySession> s, ChatClient c, String model, IOpenAiToolCache toolCache = null, PerfMonitor monitor = null)
                : base(c, model, toolCache, monitor)
            {
                S = s;
            }
            
            ConcurrentStack<QuerySession> S;

            public void Dispose()
            {
                var s = Interlocked.Exchange(ref S, null);
                if (s != null)
                    s.Push(this);
            }
        }

        QuerySession GetSession(TranslationEffort effort)
        {
            var ei = (int)effort;
            var s = Sessions[ei];
            if (s.TryPop(out var session))
                return session;
            var llm = Llm;
            var model = Models[ei];
            session = new QuerySession(s, llm.CreateChatClient(model), model ?? llm.DefaultChatModel, llm, PerfMon);
            session.SystemPrompt = SystemPrompt;
            return session;
        }

        #endregion//Session cache



        static String LangInfo(String l)
        {
            var i = IsoLanguage.TryGetName(l);
            if (i == null)
                return null;
            var c = i.Comment;
            return c == null
                ?
                String.Concat(l, " (", i.Name, ')')
                :
                String.Concat(l, " (", i.Name, " - ", c, ')');
                ;
        }

        static void AddHeader(List<String> q, String from, String to, String c)
        {


            q.Add("SourceLanguage: "  + ((String.IsNullOrEmpty(from) || from.FastEquals("*")) ? "Please determine source language from the text" : LangInfo(from)));
            q.Add("TargetLanguage: " + LangInfo(to));
            if (!String.IsNullOrEmpty(c))
                q.Add("Context: " + c);
        }

        static void SetTarget(List<String> q, String to)
        {
            q[1] = ("TargetLanguage: " + LangInfo(to));
        }

        static bool AddBlock(List<String> q, String text)
        {
            text = text?.Trim();
            if (String.IsNullOrEmpty(text))
                return false;
            bool isMd = text.FastStartsWith(MD);
            if (isMd)
            {
                text = text.Substring(4).Trim();
                if (String.IsNullOrEmpty(text))
                    return false;
                q.Add("== MD ==");
            }
            else
            {
                q.Add("== TEXT ==");
            }
            q.Add(text);
            return true;
        }


        static List<String> Decode(String response)
        {
            response = response.Trim();
            if (!response.FastStartsWith("=="))
                return null;
            List<String> res = new List<string>();
            var len = response.Length;
            int start = 0;
            for (start = 0; start < len; )
            {
                var r = response.IndexOf("== ", start);
                if (r < 0)
                    break;
                if (response.FastStartsWith("== MD ==", r))
                {
                    var size = r - start;
                    if (size > 0)
                        res.Add(response.Substring(start, size).Trim());
                    start = r + 8;
                }else
                {
                    if (response.FastStartsWith("== TEXT ==", r))
                    {
                        var size = r - start;
                        if (size > 0)
                            res.Add(response.Substring(start, size).Trim());
                        start = r + 10;
                    }
                }
            }
            var sizeEnd = len - start;
            if (sizeEnd > 0)
                res.Add(response.Substring(start, sizeEnd).Trim());
            return res;
        }

        Task CountTokens0(String model, long input, long output)
        {
            Interlocked.Add(ref CountInputTokens[0], input);
            Interlocked.Add(ref CountOutputTokens[0], output);
            return Task.CompletedTask;
        }

        Task CountTokens1(String model, long input, long output)
        {
            Interlocked.Add(ref CountInputTokens[1], input);
            Interlocked.Add(ref CountOutputTokens[1], output);
            return Task.CompletedTask;
        }

        Task CountTokens2(String model, long input, long output)
        {
            Interlocked.Add(ref CountInputTokens[2], input);
            Interlocked.Add(ref CountOutputTokens[2], output);
            return Task.CompletedTask;
        }


        public long[] InputTokens => [Interlocked.Read(ref CountInputTokens[0]), Interlocked.Read(ref CountInputTokens[1]), Interlocked.Read(ref CountInputTokens[2])];
        public long[] OutputTokens => [Interlocked.Read(ref CountOutputTokens[0]), Interlocked.Read(ref CountOutputTokens[1]), Interlocked.Read(ref CountOutputTokens[2])];

        long[] CountInputTokens = new long[3];
        
        long[] CountOutputTokens = new long[3];

        readonly Func<String, long, long, Task>[] CountTokensTasks = new Func<string, long, long, Task>[3];


        public Task<string[]> Translate(TranslateRequest request)
            => Translate(request.Text, request.To, request.From, request.Context, request.Effort, request.Retention);

        public Task<string[]> TranslateMultiple(TranslateMultipleRequest request)
            => TranslateMultiple(request.Texts, request.To, request.From, request.Context, request.Effort, request.Retention);

        public Task<string> TranslateOne(TranslateRequest request)
            => TranslateOne(request.Text, request.To, request.From, request.Context, request.Effort, request.Retention);

        volatile SupLang Languages;

        sealed class SupLangSave
        {
            public DateTime Updated;
            public String[] ValidList;

        }

        sealed class SupLang
        {
            public SupLang(SupLangSave s)
            {
                Updated = s.Updated;
                var l = s.ValidList;
                ValidList = l;
                ValidSet = l.ToHashSet(StringComparer.Ordinal).Freeze();
            }
            public readonly DateTime Updated;
            public readonly IReadOnlyList<String> ValidList;
            public readonly IReadOnlySet<String> ValidSet;

        }

        async Task<SupLang> InternalLanguagesAsync()
        {
            var l = Languages;
            if (l != null)
                return l;
            await TryGetLanguagesNow().ConfigureAwait(false);
            return Languages;
        }

        SupLang InternalLanguages()
        {
            var l = Languages;
            if (l != null)
                return l;
            TryGetLanguagesNow().RunAsync();
            return Languages;
        }


        static readonly Char[] SplitOn = "\n\r ".ToCharArray();
        
        async Task TryGetLanguagesNow(bool forceRenew = false)
        {
            const int modelIndex = 2; // Low, Med, High
            try
            {
                var model = Models[modelIndex];
                string storeKey = "LLmTranslator.Lang." + model;
                var current = await KeyValueStore.AllShared.TryGetAsync<SupLangSave>(storeKey).ConfigureAwait(false);
                if ((current == null) || forceRenew || ((DateTime.UtcNow - current.Updated) > TimeSpan.FromDays(30)))
                {
                    String[] tt = null;
                    DateTime updated = DateTime.UtcNow;
                    try
                    {
                        var q = Llm.CreateQuerySession(model);
                        var res = await q.Query(GetLanguagesPrompt, null, CountTokensTasks[modelIndex]).ConfigureAwait(false);
                        if (!res.FastStartsWith("Error:"))
                        {
                            tt = res.Split(SplitOn, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                            var l = tt.Length;
                            var s = new HashSet<String>(l, StringComparer.Ordinal);
                            for (int i = 0; i < l; ++i)
                            {
                                var lang = tt[i];
                                var lc = IsoLanguage.TryGet(out var c, lang);
                                if (lc != null)
                                {
                                    if (c == null)
                                        s.Add(lc.Iso639_1);
                                    else
                                        s.Add(String.Concat(lc.Iso639_1, '-', c.Iso3166a2));
                                }
                                else
                                {
                                    lc = IsoLanguage.TryGetName(lang);
                                    if (lc != null)
                                        s.Add(lc.Iso639_1);
                                    else
                                    {
                                        s.Add(lang);
                                    }
                                }

                            }
                            tt = s.OrderBy(x => x).ToArray();
                            updated = DateTime.UtcNow;
                        }
                    }
                    catch
                    {
                    }
                    if (tt == null)
                    {
                        tt = IsoLanguage.Common;
                        updated = DateTime.UtcNow.AddDays(-100);
                    }
                    current = new SupLangSave
                    {
                        Updated = updated,
                        ValidList = tt,
                    };
                    await KeyValueStore.AllShared.SetAsync(storeKey, current).ConfigureAwait(false);
                }
                Interlocked.Exchange(ref Languages, new SupLang(current));
            }
            catch
            {
            }
        }

        public async Task<IReadOnlyList<String>> GetSupportedSourceLanguages()
        {
            var lang = await InternalLanguagesAsync().ConfigureAwait(false);
            return lang?.ValidList;
        }

        public Task<IReadOnlyList<String>> GetSupportedTargetLanguages()
            => GetSupportedSourceLanguages();

        static bool IsValid(ref String val, IReadOnlySet<String> set)
        {
            if (set == null)
                return false;
            if (set.Contains(val))
                return true;
            var i = val.IndexOf('-');
            if (i < 0)
                return false;
            var t = val.Substring(0, i);
            if (!val.Contains(t))
                return false;
            val = t;
            return true;
        }

        public async Task<string> CanFrom(string from)
        {
            var lang = await InternalLanguagesAsync().ConfigureAwait(false);
            if (lang == null)
                return null;
            if (!IsValid(ref from, lang.ValidSet))
                return null;
            return from;
        }

        public Task<string> CanTo(string to)
            => CanFrom(to);

        public IEnumerable<Stats> GetStats()
        {
            yield return new Stats(nameof(LlmTranslator), "Low effort input tokens", Interlocked.Read(ref CountInputTokens[0]), "The approximate number of input tokens used for the low effort model");
            yield return new Stats(nameof(LlmTranslator), "Low effort output tokens", Interlocked.Read(ref CountOutputTokens[0]), "The approximate number of output tokens used for the low effort model");
            yield return new Stats(nameof(LlmTranslator), "Medium effort input tokens", Interlocked.Read(ref CountInputTokens[1]), "The approximate number of input tokens used for the high effort model");
            yield return new Stats(nameof(LlmTranslator), "Medium effort output tokens", Interlocked.Read(ref CountOutputTokens[1]), "The approximate number of output tokens used for the high effort model");
            yield return new Stats(nameof(LlmTranslator), "High effort input tokens", Interlocked.Read(ref CountInputTokens[2]), "The approximate number of input tokens used for the medium effort model");
            yield return new Stats(nameof(LlmTranslator), "High effort output tokens", Interlocked.Read(ref CountOutputTokens[2]), "The approximate number of output tokens used for the medium effort model");
        }


        public void Dispose()
        {
        }

        #region IInternalTranslator

        public async Task<string[]> Translate(string text, string to, string from, string context, TranslationEffort effort, TranslationCacheRetention retention)
        {
            var dest = to.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var count = dest.Length;
            if (text.FastStartsWith(TranslationTools.NoTranslatePrefix))
            {
                text = text.Substring(TranslationTools.NoTranslatePrefixLength);
                return ArrayExt.Create(count, text);
            }
            if (count <= 0)
                return Array.Empty<String>();

            using var client = GetSession(effort);
            List<String> q = new List<string>(16);
            AddHeader(q, from, null, context);
            if (!AddBlock(q, text))
                return Array.Empty<String>();
            var ret = new String[count];
            var countTask = CountTokensTasks[(int)effort];
            var retryCount = RetryCount;
            for (int i = 0; i < count; ++i)
            {
                SetTarget(q, dest[i]);
                List<String> texts;
                for (int retry = 0; ; ++retry)
                {
                    String res;
                    using (var x = PerfMon.Track("Query." + effort))
                        res = await client.Query(String.Join('\n', q), null, countTask).ConfigureAwait(false);
                    texts = Decode(res);
                    if (texts != null)
                        break;
                    if (retry >= retryCount)
                        break;
                }
                ret[i] = (texts?.Count ?? 0) > 0 ? texts[0] : null;
            }
            return ret;
        }

        readonly int RetryCount;

        public async Task<string[]> TranslateMultiple(string[] texts, string to, string from, string context, TranslationEffort effort, TranslationCacheRetention retention)
        {
            var dest = to.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var count = dest.Length;
            if (count <= 0)
                return Array.Empty<String>();
            using var client = GetSession(effort);
            List<String> q = new List<string>(16);
            AddHeader(q, from, null, context);
            int textCount = 0;
            foreach (var t in texts)
            {
                if (t.FastStartsWith(TranslationTools.NoTranslatePrefix))
                    continue;
                if (AddBlock(q, t))
                    ++textCount;
            }
            if (textCount <= 0)
                return Array.Empty<String>();

            String[] ret = new string[count * textCount];
            var countTask = CountTokensTasks[(int)effort];
            var retryCount = RetryCount;
            for (int i = 0, o = 0; i < count; ++i)
            {
                SetTarget(q, dest[i]);
                List<String> otexts;
                for (int retry = 0; ; ++retry)
                {
                    String res;
                    using (var x = PerfMon.Track("Query." + effort))
                        res = await client.Query(String.Join('\n', q), null, countTask).ConfigureAwait(false);
                    otexts = Decode(res);
                    if (otexts != null)
                        break;
                    if (retry >= retryCount)
                        break;
                }


                var tl = otexts?.Count ?? 0;
                for (int j = 0; j < textCount; ++j, ++o)
                {
                    var text = texts[j];
                    if (text.FastStartsWith(TranslationTools.NoTranslatePrefix))
                        ret[o] = text.Substring(TranslationTools.NoTranslatePrefixLength);
                    else
                        ret[o] = j < tl ? otexts[j] : null;
                }
            }
            return ret;
        }

        public async Task<string> TranslateOne(string text, string to, string from, string context, TranslationEffort effort, TranslationCacheRetention retention)
        {
            if (text.FastStartsWith(TranslationTools.NoTranslatePrefix))
                return text.Substring(TranslationTools.NoTranslatePrefixLength);
            using var client = GetSession(effort);
            List<String> q = new List<string>(16);
            AddHeader(q, from, to, context);
            if (!AddBlock(q, text))
                return null;
            var retryCount = RetryCount;
            List<String> texts;
            for (int retry = 0; ; ++retry)
            {
                String res;
                using (var x = PerfMon.Track("Query." + effort))
                    res = await client.Query(String.Join('\n', q), null, CountTokensTasks[(int)effort]).ConfigureAwait(false);
                texts = Decode(res);
                if (texts != null)
                    break;
                if (retry >= retryCount)
                    break;
            }
            return (texts?.Count ?? 0) > 0 ? texts[0] : null;
        }

        public Task<string> RequestOne(string from, string to, string text, string context, TranslationEffort effort, TranslationCacheRetention retention)
            => TranslateOne(text, to, from, context, effort, retention);

        public IReadOnlyList<string> SupportedSourceLanguages()
        {
            var lang = InternalLanguages();
            return lang?.ValidList;
        }

        public IReadOnlyList<string> SupportedTargetLanguages()
            => SupportedSourceLanguages();

        public string CanTranslateFrom(string from)
        {
            var lang = InternalLanguages();
            if (lang == null)
                return null;
            if (!IsValid(ref from, lang.ValidSet))
                return null;
            return from;
        }

        public string CanTranslateTo(string to)
            => CanTranslateFrom(to);


        #endregion//IInternalTranslator


        const String SystemPrompt = @"
You are an expert at translating text and respecting the context and details of the instructions.
The text to translate can be plain text or MD (mark down) encoded text.
Text can contain arguments/parameters that is replaced when used.
Parameters start with a '{' (or '${') and end with a '}', ex: ""Hello {0}!"", in this case we can assume that ""{0}"" will be replaced by a name.
Text within these parameters should not be translated.
When the text is MD, make sure to not translate any links or other control codes.

A translation request have the following syntax:
------------------------------------
SourceLanguage: <lang>
TargetLanguage: <lang>
Context: <context>
== TEXT ==
<plain text>
== TEXT ==
<plain text>
== MD ==
<md text>
== MD ==
<md text>
------------------------------------

<lang> is the source language (LCID, ISO code or plain text).
<context> is some per translation context, that can give hints to what context the text is used, entities in the source text that shouldn't be translated etc.
<plain text> is some plain text that should be translated.
<md text> is some mark down text that should be translated.
There can be any number or combination of ""== TEXT =="" and ""== MD =="" blocks.

The response should only contain the translations, no explanations etc.
The output format should separate each translated block using ""== TEXT =="" or ""== MD =="", similar to the input.
";


        const String GetLanguagesPrompt = @"
List all languages that you understand.
Respond using plain text with one language per row.
Output the language LCID (locale code), not the name.
Some examples: ""en"", ""en-US"", ""en-GB"", ""es"", ""es-MX"", ""es-ES"", ""sv"", ""sv-FI"", ""pt"" and ""pt-BR"".
Make sure to only output each language once.
Output only this list, no explanation or anything else.
Make sure to output ALL languages that you grasp, not just a few common.
";


    }
}
