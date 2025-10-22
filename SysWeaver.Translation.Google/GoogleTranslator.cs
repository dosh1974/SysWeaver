using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


using SysWeaver.IsoData;

namespace SysWeaver.Translation
{

    /// <summary>
    /// A translator that (ab)uses a google web page to perform translation.
    /// Will run into rate limiting (error 429) after too many request.
    /// Can automatically switch to TOR to avoid the 429 but at lower speed.
    /// </summary>
    public sealed class GoogleTranslator : IPerfMonitored, IHaveStats, IInternalTranslator
    {
        public GoogleTranslator(GoogleTranslatorParams p = null)
        {
            p = p ?? new GoogleTranslatorParams();
            PerfMon.Enabled = p.PerMon;
            if (p.UseTor)
            {
                if (TorService.IsAvailable)
                {
                    CanUseTor = true;
                    if (p.StartTor)
                        WebTools.GetHttpClient(30, true);
                }
            }
            UseTorFor = TimeSpan.FromMinutes(Math.Min(1, p.UseTorFor)).Ticks;
            MaxRetry = Math.Max(0, Math.Min(100, p.Retry));
            MemCaches[0] = new FastMemCache<String, String>(TimeSpan.FromSeconds(Math.Max(10, p.ShortMemCacheDuration)));
            MemCaches[1] = new FastMemCache<String, String>(TimeSpan.FromSeconds(Math.Max(10, p.MediumMemCacheDuration)));
            MemCaches[2] = new FastMemCache<String, String>(TimeSpan.FromSeconds(Math.Max(10, p.LongMemCacheDuration)));
            UpdateSupportedLanguages().RunAsync();
            UpdateSupportedLanguagesTask = new PeriodicTask(UpdateSupportedLanguages, 60000);
        }

        readonly bool CanUseTor;

        /// <summary>
        /// Translate some text to one or more languages
        /// </summary>
        /// <param name="request">Paramaters</param>
        /// <returns>Translations in the same order as specified in the parameters</returns>
        public Task<string[]> Translate(TranslateRequest request)
        {
            var f = ValidateFrom(request.From);
            var t = ValidateTo(request.To);
            var m = ValidateMessage(request.Text);
            return Do(f, t, m, request.Effort, request.Retention);
        }

        /// <summary>
        /// Translate some text to one or more languages
        /// </summary>
        /// <param name="text">The text to translate</param>
        /// <param name="to">The two letter ISO-639-1 language code of the target language (with an optional two letter ISO-3166-a2 country code appended with a hyphen, ex: "zw-CH").
        /// Multiple targets can be set by using a comma separation.</param>
        /// <param name="from">The two letter ISO-639-1 language code of the source language (the supplied text).
        /// "*" can be used to let the translator identify the source language.</param>
        /// <param name="context"></param>
        /// <param name="effort">The effort to use when translating</param>
        /// <param name="retention">The duration to cache the translation</param>
        /// <returns>Translations in the same order as specified in the parameters</returns>
        public Task<string[]> Translate(string text, string to, string from, String context, TranslationEffort effort, TranslationCacheRetention retention)
        {
            var f = ValidateFrom(from);
            var t = ValidateTo(to);
            var m = ValidateMessage(text);
            return Do(f, t, m, effort, retention);
        }

        /// <summary>
        /// Translate multiple texts to one or more languages
        /// </summary>
        /// <param name="request">Paramaters</param>
        /// <returns>Translations in the same order as specified in the parameters</returns>
        public Task<string[]> TranslateMultiple(TranslateMultipleRequest request)
        {
            var f = ValidateFrom(request.From);
            var t = ValidateTo(request.To);
            var m = ValidateMessages(request.Texts);
            return Do(f, t, m, request.Effort, request.Retention);
        }

        /// <summary>
        /// Translate multiple texts to one or more languages
        /// </summary>
        /// <param name="texts">The texts to translate</param>
        /// <param name="to">The two letter ISO-639-1 language code of the target language (with an optional two letter ISO-3166-a2 country code appended with a hyphen, ex: "zw-CH").
        /// Multiple targets can be set by using a comma separation.</param>
        /// <param name="from">The two letter ISO-639-1 language code of the source language (the supplied text).
        /// "*" can be used to let the translator identify the source language.</param>
        /// <param name="context"></param>
        /// <param name="effort">The effort to use when translating</param>
        /// <param name="retention">The duration to cache the translation</param>
        /// <returns>Translations in the same order as specified in the parameters</returns>
        public Task<string[]> TranslateMultiple(string[] texts, string to, string from, String context, TranslationEffort effort, TranslationCacheRetention retention)
        {
            var f = ValidateFrom(from);
            var t = ValidateTo(to);
            var m = ValidateMessages(texts);
            return Do(f, t, m, effort, retention);
        }

        /// <summary>
        /// Translate some text to a new language
        /// </summary>
        /// <param name="request">Paramaters</param>
        /// <returns>Translated text</returns>
        public async Task<string> TranslateOne(TranslateRequest request)
        {
            var f = ValidateFrom(request.From);
            var t = ValidateToSingle(request.To);
            var m = ValidateMessage(request.Text);
            return (await Do(f, t, m, request.Effort, request.Retention).ConfigureAwait(false))[0];
        }

        /// <summary>
        /// Translate some text to a new language
        /// </summary>
        /// <param name="text">The text to translate</param>
        /// <param name="to">The two letter ISO-639-1 language code of the target language (with an optional two letter ISO-3166-a2 country code appended with a hyphen, ex: "zw-CH").</param>
        /// <param name="from">The two letter ISO-639-1 language code of the source language (the supplied text).
        /// "*" can be used to let the translator identify the source language.</param>
        /// <param name="context"></param>
        /// <param name="effort">The effort to use when translating</param>
        /// <param name="retention">The duration to cache the translation</param>
        /// <returns>Translated text</returns>
        public async Task<string> TranslateOne(string text, string to, string from, String context, TranslationEffort effort, TranslationCacheRetention retention)
        {
            var f = ValidateFrom(from);
            var t = ValidateToSingle(to);
            var m = ValidateMessage(text);
            return (await Do(f, t, m, effort, retention).ConfigureAwait(false))[0];
        }


        static bool IsValid(ref String val, HashSet<String> set)
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


        public String CanTranslateFrom(String from)
        {
            var sl = SourceLangs;
            if (IsValid(ref from, sl))
                return from;
            if (sl != null)
                return null;
            var li = IsoLanguage.TryGet(out var lang, from);
            if (li == null)
                return null;
            return lang == null ? li.Iso639_1 : String.Join('-', li.Iso639_1, lang.Iso3166a2);
        }

        public String CanTranslateTo(String to)
        {
            var sl = TargetLangs;
            if (IsValid(ref to, sl))
                return to;
            if (sl != null)
                return null;
            var li = IsoLanguage.TryGet(out var lang, to);
            if (li == null)
                return null;
            return lang == null ? li.Iso639_1 : String.Join('-', li.Iso639_1, lang.Iso3166a2);
        }


        String ValidateFrom(String from)
        {
            if ((from == "*") || String.IsNullOrEmpty(from))
                return "auto";
            if (IsValid(ref from, SourceLangs))
                return from;
            var li = IsoLanguage.TryGet(out var lang, from);
            if (li == null)
                throw new ArgumentException("From must be a valid two letter ISO-639-1 language code, optionally combined with an ISO 3166-A2 country code using a hyphen, got \"" + from + "\"", nameof(from));
            return li.Iso639_1;
        }


        String[] ValidateToSingle(String to)
        {
            if (IsValid(ref to, SourceLangs))
                return [to];
            var li = IsoLanguage.TryGet(out var lang, to);
            if (li == null)
                throw new ArgumentException("To must be a valid two letter ISO-639-1 language code, optionally combined with an ISO 3166-A2 country code using a hyphen, got \"" + to + "\"", nameof(to));
            return [ lang == null ? li.Iso639_1 : String.Join('-', li.Iso639_1, lang.Iso3166a2) ];
        }


        String[] ValidateTo(String to)
        {
            var t = to.Split(',');
            var tl = t.Length;
            for (int i = 0; i < tl; ++i)
            {
                var s = t[i].Trim();
                if (IsValid(ref s, TargetLangs))
                {
                    t[i] = s;
                    continue;
                }
                var li = IsoLanguage.TryGet(out var lang, s);
                if (li == null)
                    throw new ArgumentException("To must be a valid two letter ISO-639-1 language code, optionally combined with an ISO 3166-A2 country code using a hyphen, or a comma sperated list of such", nameof(to));
                t[i] = lang == null ? li.Iso639_1 : String.Join('-', li.Iso639_1, lang.Iso3166a2);
            }
            return t;
        }

        static String[] ValidateMessage(String message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message), "Message may not be null!");
            message = message.Trim();
            if (message.Length <= 0)
                throw new ArgumentException("Message may not be empty!", nameof(message));
            return [message];
        }

        static String[] ValidateMessages(String[] messages)
        {
            if (messages == null)
                throw new ArgumentNullException(nameof(messages), "Messages may not be null!");
            var ml = messages.Length;
            if (ml <= 0)
                throw new ArgumentException("Messages may not be empty!", nameof(messages));
            for (int i = 0; i < ml; ++i)
            {
                var message = messages[i];
                messages[i] = message?.Trim();
            }
            return messages;
        }

        static bool IsWhiteSpace(char c)
        {
            if (!Char.IsWhiteSpace(c))
                return false;
            return (c != (Char)10) && (c != (Char)13);
        }


        /// <summary>
        /// Removes multiple white spaces since the translator will remove those
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        String Fix(String t)
        {
            if (String.IsNullOrEmpty(t))
                return t;
            StringBuilder b = new StringBuilder(t.Length);
            bool changed = false;
            bool prevIsSpace = true;
            foreach (var c in t)
            {
                bool isSpace = IsWhiteSpace(c);
                if (isSpace && prevIsSpace)
                {
                    changed = true;
                    continue;
                }
                b.Append(c);
                prevIsSpace = isSpace;
            }
            return changed ? b.ToString() : t;
        }

        async Task<String[]> Do(String from, String[] to, String[] texts, TranslationEffort effort, TranslationCacheRetention retention)
        {
            using var measure = PerfMon.Track("BatchTranslation");
            int lc = to.Length;
            int tc = texts.Length;
            var count = lc * tc;
            Task<String>[] tasks = new Task<string>[count];
            int o = 0;
            for (int t = 0; t < tc; ++t)
            {
                var text = texts[t];
                if (text.FastStartsWith(TranslationTools.NoTranslatePrefix))
                {
                    var ct = Task.FromResult(text.Substring(TranslationTools.NoTranslatePrefixLength));
                    for (int l = 0; l < lc; ++l)
                    {
                        tasks[o] = ct;
                        ++o;
                    }
                }
                else
                {

                    if (text.AnyLetter())
                    {
                        text = Fix(text);
                        for (int l = 0; l < lc; ++l)
                        {
                            tasks[o] = DoOne(from, to[l], text, effort, retention);
                            ++o;
                        }
                    }
                    else
                    {
                        var ct = Task.FromResult(text);
                        for (int l = 0; l < lc; ++l)
                        {
                            tasks[o] = ct;
                            ++o;
                        }
                    }
                }
            }
            await Task.WhenAll(tasks).ConfigureAwait(false);
            String[] res = GC.AllocateUninitializedArray<String>(count);
            for (int i = 0; i < count; ++i)
                res[i] = tasks[i].Result;
            return res;
        }


        async Task<String> InternalOne(String url, String to, String from, String text, String q)
        {
            if (text.FastStartsWith(TranslationTools.NoTranslatePrefix))
                return text.Substring(TranslationTools.NoTranslatePrefixLength);
            var res = await DoReq(url).ConfigureAwait(false);
            if (to.Length > 2)
            {
                var maxForSim = (text.Length + 1) >> 1;
                if (maxForSim > 8)
                    maxForSim = 8;
                var dist = StringTools.Levenstein(text, res);
                if (dist <= maxForSim)
                {
                    //  Probably failed, translate using two letter iso instead
                    var url2 = "https://translate.google.com/m?tl=" + to.Substring(0, 2) + "&sl=" + from + "&q=" + q;
                    res = await DoReq(url2).ConfigureAwait(false);
                }
            }
            if (res.Length > 0)
            {
                if (Char.IsUpper(res[0]))
                {
                    var c = res[0];
                    if (!Char.IsUpper(c))
                        res = Char.ToUpper(c) + res.Substring(1);
                }
            }
            return res;
        }

        /// <summary>
        /// Perform a request by passing any caches, you probably shouldn't use this!
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="text"></param>
        /// <param name="context"></param>
        /// <param name="effort"></param>
        /// <param name="retention"></param>
        /// <returns></returns>
        public Task<String> RequestOne(String from, String to, String text, String context, TranslationEffort effort, TranslationCacheRetention retention)
        {
            var q = Uri.EscapeDataString(text);
            var url = "https://translate.google.com/m?tl=" + to + "&sl=" + from + "&q=" + q;
            return InternalOne(url, to, from, text, q);
        }

        
        /// <summary>
        /// 0 to not use tor, else the start tick for using tor
        /// </summary>
        long TorUsageStarted;

        /// <summary>
        /// Number of times we've switched on tor usage
        /// </summary>
        long TorUsageCount;

        /// <summary>
        /// Number of ticks to use tor, before trying without it
        /// </summary>
        readonly long UseTorFor;

        /// <summary>
        /// Number of times to retry translation request before giving up
        /// </summary>
        readonly int MaxRetry;

        async Task<String> RequestWithOptionalTor(String url)
        {
            int maxRetry = MaxRetry;
            long torStarted = Interlocked.Read(ref TorUsageStarted);
            if (torStarted != 0)
            {
                if ((DateTime.UtcNow.Ticks - torStarted) > UseTorFor)
                    Interlocked.CompareExchange(ref TorUsageStarted, 0, torStarted);
            }
            String res;
            for (int retry = 0; ; ++retry)
            {
                var useTor = Interlocked.Read(ref TorUsageStarted) != 0;
                var client = WebTools.GetHttpClient(useTor ? TorTimeout : RegTimeout, useTor);
                try
                {
                    res = await client.GetStringAsync(url).ConfigureAwait(false);
                    break;
                }
                catch (HttpRequestException rex)
                {
                    if ((!useTor) && CanUseTor)
                    {
                        if (rex.StatusCode == HttpStatusCode.TooManyRequests)
                        {
                            if (Interlocked.CompareExchange(ref TorUsageStarted, DateTime.UtcNow.Ticks, 0) == 0)
                                Interlocked.Increment(ref TorUsageCount);
                            retry = 0;
                            continue;
                        }
                    }
                    if (retry >= maxRetry)
                        throw;
                }
                catch
                {
                    if (retry >= maxRetry)
                        throw;
                }
                await Task.Delay(Math.Min(500, retry * 50 + 10)).ConfigureAwait(false);
            }
            return res;
        }

        async Task<String> DoReq(String url)
        {
            using var measure = PerfMon.Track("GoogleTranslate");
            var res = await RequestWithOptionalTor(url).ConfigureAwait(false);
            var x = res.LastIndexOf(TranslateBegin);
            if (x < 0)
                return null;
            x += TranslateBeginL;
            var e = res.IndexOf(TranslateEnd, x);
            if (e < 0)
                return null;
            res = res.Substring(x, e - x);
            res = WebUtility.HtmlDecode(res);
            return res;
        }

        Task<String> DoOne(String from, String to, String text, TranslationEffort effort, TranslationCacheRetention retention)
        {
            if (from == to)
                return Task.FromResult(text);
            var q = Uri.EscapeDataString(text);
            var url = "https://translate.google.com/m?tl=" + to + "&sl=" + from + "&q=" + q;
            return MemCaches[(int)retention].GetOrUpdateAsync(url, valUrl => InternalOne(valUrl, to, from, text, q));
        }


        const String StatsName = nameof(GoogleTranslator);

        static readonly String[] CacheNames = [
                "Cache.Short.",
                "Cache.Medium.",
                "Cache.Long.",
            ];

        public IEnumerable<Stats> GetStats()
        {
            yield return new Stats(StatsName, "CanUseTor", CanUseTor, "True if tor can be used");
            yield return new Stats(StatsName, "IsUsingTor", Interlocked.Read(ref TorUsageStarted) != 0, "True if tor is being used right now");
            yield return new Stats(StatsName, "TorCount", Interlocked.Read(ref TorUsageCount), "Number of times we've switched to use the tor network");
           
            foreach (var x in LangFails.GetStats(StatsName, ""))
                yield return x;
            for (int i = 0; i < 3; ++i)
                foreach (var x in MemCaches[i].GetStats(StatsName, CacheNames[i]))
                    yield return x;
        }


        const String TranslateBegin = "<div class=\"result-container\">";
        const String TranslateEnd = "</div>";

        static readonly int TranslateBeginL = TranslateBegin.Length;


        readonly FastMemCache<String, String>[] MemCaches = new FastMemCache<string, string>[3];



        HashSet<String> SourceLangs;
        HashSet<String> TargetLangs;


        /// <summary>
        /// Parse languages from retruned html
        /// </summary>
        /// <param name="html">The html code</param>
        /// <param name="isSource">True if this is a source languge, else its a target language</param>
        /// <returns>Set of supported languages</returns>
        static HashSet<String> ParseLanguages(String html, bool isSource)
        {
            const String sourceH = "<a href=\"./m?sl=";
            const String end = "&amp;";
            const String targetH = "&amp;tl=";
            var start = isSource ? sourceH : targetH;
            var startL = start.Length;
            var endL = end.Length;
            var l = html.Length;
            int o = 0;
            HashSet<String> langs = new HashSet<string>(StringComparer.Ordinal);
            while (o < l)
            {
                o = html.IndexOf(start, o);
                if (o < 0)
                    break;
                o += startL;
                var e = html.IndexOf(end, o);
                if (e < 0)
                    continue;
                langs.Add(html.Substring(o, e - o));
                o = e + endL;
            }
            langs.Remove("auto");
            return langs;
        }

        const int TorTimeout = 120;
        const int RegTimeout = 30;

        async ValueTask<bool> UpdateSupportedLanguages()
        {
            try
            {
                foreach (var c in MemCaches)
                    c.Prune();
            }
            catch
            {
            }
            if ((DateTime.UtcNow - LastSuccess) < TimeSpan.FromHours(1))
                return true;
            using var measure = PerfMon.Track(nameof(UpdateSupportedLanguages));
            try
            {
                var res = await RequestWithOptionalTor("https://translate.google.com/m?mui=sl&hl=en").ConfigureAwait(false);
                SourceLangs = ParseLanguages(res, true);
                res = await RequestWithOptionalTor("https://translate.google.com/m?mui=tl&hl=en").ConfigureAwait(false);
                TargetLangs = ParseLanguages(res, false);
                LastSuccess = DateTime.UtcNow;
            }
            catch (Exception e)
            {
                LangFails.OnException(e);
            }
            return true;
        }

        readonly ExceptionTracker LangFails = new ExceptionTracker();
        
        PeriodicTask UpdateSupportedLanguagesTask;

        DateTime LastSuccess;

        public PerfMonitor PerfMon { get; private set; } = new PerfMonitor(nameof(GoogleTranslator));


        public void Dispose()
        {
            Interlocked.Exchange(ref UpdateSupportedLanguagesTask, null)?.Dispose();
        }

        /// <summary>
        /// Return a list of supported source languages
        /// </summary>
        /// <returns>A list of supported source languages</returns>
        public IReadOnlyList<String> SupportedSourceLanguages() => SourceLangs.ToArray();


        /// <summary>
        /// Return a list of supported target languages
        /// </summary>
        /// <returns>A list of supported target languages</returns>
        public IReadOnlyList<String> SupportedTargetLanguages() => TargetLangs.ToArray();

        /// <summary>
        /// Return a list of supported source languages
        /// </summary>
        /// <returns>A list of supported source languages</returns>
        public Task<IReadOnlyList<String>> GetSupportedSourceLanguages()
            => Task.FromResult(SupportedSourceLanguages());

        /// <summary>
        /// Return a list of supported target languages
        /// </summary>
        /// <returns>A list of supported target languages</returns>
        public Task<IReadOnlyList<String>> GetSupportedTargetLanguages()
            => Task.FromResult(SupportedTargetLanguages());

        /// <summary>
        /// Returns a formatted from language if it's valid, else null
        /// </summary>
        /// <param name="from"></param>
        /// <returns></returns>
        public Task<String> CanFrom(String from)
            => Task.FromResult(CanTranslateFrom(from));

        /// <summary>
        /// Returns a formatted to language if it's valid, else null
        /// </summary>
        /// <param name="to"></param>
        /// <returns></returns>
        public Task<String> CanTo(String to)
            => Task.FromResult(CanTranslateTo(to));


    }




}
