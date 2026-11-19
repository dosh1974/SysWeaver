using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SysWeaver.Translation
{
    /// <summary>
    /// A translator for tests: no outside service, no cost, no rate limit. A translation is the text with the target language in front,
    /// "[de] Which animal is this?", so translate buttons, field updates and undo can be tested offline and the translated texts are easy
    /// to spot. Translating a mock translation again changes the language tag (no "[de] [sv] ..").
    /// Texts that start with the no translate prefix are returned without it, as a real translator does.
    /// </summary>
    public sealed class MockTranslator : IInternalTranslator
    {
        public MockTranslator(MockTranslatorParams p = null)
        {
            p = p ?? new MockTranslatorParams();
            Langs = (p.Languages ?? MockTranslatorParams.DefaultLanguages).Where(x => !String.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToArray();
            Delay = Math.Max(0, p.Delay);
            LogContext = p.LogContext;
        }

        readonly String[] Langs;
        readonly int Delay;
        readonly bool LogContext;

        static String One(String text, String to)
        {
            if (String.IsNullOrEmpty(text))
                return text;
            if (text.StartsWith(TranslationTools.NoTranslatePrefix, StringComparison.Ordinal))
                return text.Substring(TranslationTools.NoTranslatePrefix.Length);
            var t = (to ?? "").Split(',')[0].Trim();
            //  (a text that already is a mock translation gets the new language, not two tags)
            if ((text.Length > 4) && (text[0] == '[') && (text[3] == ']') && (text[4] == ' '))
                text = text.Substring(5);
            return String.Concat("[", t, "] ", text);
        }

        async Task<String[]> Many(IEnumerable<String> texts, String to, String context)
        {
            var src = texts?.ToArray() ?? [];
            if (LogContext)
                Console.WriteLine(String.Concat("MockTranslator: ", src.Length, " text(s) to ", to, ", context: ", context ?? "(none)", src.Length > 0 ? String.Concat(", first: ", src[0]) : ""));
            if (Delay > 0)
                await Task.Delay(Delay).ConfigureAwait(false);
            var targets = (to ?? "").Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
            var res = new List<String>();
            foreach (var t in targets.Length > 0 ? targets : ["en"])
                res.AddRange(src.Select(x => One(x, t)));
            return res.ToArray();
        }

        public Task<string[]> Translate(TranslateRequest request) => Many([request.Text], request.To, request.Context);
        public Task<string[]> TranslateMultiple(TranslateMultipleRequest request) => Many(request.Texts, request.To, request.Context);
        public async ValueTask<string> TranslateOne(TranslateRequest request) => (await Many([request.Text], request.To, request.Context).ConfigureAwait(false))[0];
        public Task<IReadOnlyList<string>> GetSupportedSourceLanguages() => Task.FromResult<IReadOnlyList<string>>(Langs);
        public Task<IReadOnlyList<string>> GetSupportedTargetLanguages() => Task.FromResult<IReadOnlyList<string>>(Langs);
        public Task<string> CanFrom(string from) => Task.FromResult(from);
        public Task<string> CanTo(string to) => Task.FromResult(to);

        public Task<string[]> Translate(string text, string to, string from = "en", string context = null, TranslationEffort effort = TranslationEffort.Medium, TranslationCacheRetention retention = TranslationCacheRetention.Medium, TranslationContentTypes contentType = TranslationContentTypes.Text)
            => Many([text], to, context);
        public Task<string[]> TranslateMultiple(string[] texts, string to, string from = "en", string context = null, TranslationEffort effort = TranslationEffort.Medium, TranslationCacheRetention retention = TranslationCacheRetention.Medium, TranslationContentTypes contentType = TranslationContentTypes.Text)
            => Many(texts, to, context);
        public async Task<string> TranslateOne(string text, string to, string from = "en", string context = null, TranslationEffort effort = TranslationEffort.Medium, TranslationCacheRetention retention = TranslationCacheRetention.Medium, TranslationContentTypes contentType = TranslationContentTypes.Text)
            => (await Many([text], to, context).ConfigureAwait(false))[0];
        public async Task<string> RequestOne(string from, string to, string text, string context = null, TranslationEffort effort = TranslationEffort.Medium, TranslationCacheRetention retention = TranslationCacheRetention.Medium, TranslationContentTypes contentType = TranslationContentTypes.Text)
            => (await Many([text], to, context).ConfigureAwait(false))[0];

        public IReadOnlyList<string> SupportedSourceLanguages() => Langs;
        public IReadOnlyList<string> SupportedTargetLanguages() => Langs;
        public string CanTranslateFrom(string from) => from;
        public string CanTranslateTo(string to) => to;

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// The parameters of the mock translator
    /// </summary>
    public sealed class MockTranslatorParams
    {
        public static readonly String[] DefaultLanguages = ["en", "sv", "de", "fr", "es", "it", "nl", "da", "no", "fi", "pl", "pt", "ja", "zh", "ar"];

        /// <summary>
        /// The languages it "translates" from and to (two letter codes), null for a common set
        /// </summary>
        public String[] Languages;

        /// <summary>
        /// Wait this long (milliseconds) before each answer, to test how a page shows a slow translation (0 = none)
        /// </summary>
        public int Delay;

        /// <summary>
        /// Write each request (the number of texts, the target, the context and the first text) to the console, to check the context
        /// that callers give
        /// </summary>
        public bool LogContext = true;
    }
}
