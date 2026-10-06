using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SysWeaver.Net;
using SysWeaver.Translation;

namespace SysWeaver
{

    /// <summary>
    /// Translation helpers that never fail (the source text is returned on any error).
    /// </summary>
    public static class TranslatorExt
    {
        /// <summary>
        /// Translate html text, see <see cref="TranslateSafe"/>.
        /// </summary>
        /// <param name="translator">The translator, may be null (no translation)</param>
        /// <param name="htmlText">The html text to translate</param>
        /// <param name="to">The language to translate to, null for no translation</param>
        /// <param name="from">The language of the text</param>
        /// <param name="context">Optional context that helps the translation</param>
        /// <param name="effort">The translation effort</param>
        /// <param name="retention">How long the translation should be cached</param>
        /// <returns>The translated text, or the source text if translation isn't possible</returns>
        public static Task<String> TranslateSafeHtml(this ITranslator translator, String htmlText, String to, String from = "en", String context = null, TranslationEffort effort = TranslationEffort.High, TranslationCacheRetention retention = TranslationCacheRetention.Long)
            => TranslateSafe(translator, htmlText, to, from, context, effort, retention, TranslationContentTypes.Html);

        /// <summary>
        /// Translate markdown text, see <see cref="TranslateSafe"/>.
        /// </summary>
        /// <param name="translator">The translator, may be null (no translation)</param>
        /// <param name="htmlText">The markdown text to translate</param>
        /// <param name="to">The language to translate to, null for no translation</param>
        /// <param name="from">The language of the text</param>
        /// <param name="context">Optional context that helps the translation</param>
        /// <param name="effort">The translation effort</param>
        /// <param name="retention">How long the translation should be cached</param>
        /// <returns>The translated text, or the source text if translation isn't possible</returns>
        public static Task<String> TranslateSafeMD(this ITranslator translator, String htmlText, String to, String from = "en", String context = null, TranslationEffort effort = TranslationEffort.High, TranslationCacheRetention retention = TranslationCacheRetention.Long)
    => TranslateSafe(translator, htmlText, to, from, context, effort, retention, TranslationContentTypes.MarkDown);

        /// <summary>
        /// Translate a text, leading and trailing white space is kept as is (only the trimmed text is translated).
        /// The text is returned as is if the translator is null, the text is null / empty / white space, <paramref name="to"/> is null or equal to <paramref name="from"/>, or the translation fails.
        /// </summary>
        /// <param name="translator">The translator, may be null (no translation)</param>
        /// <param name="text">The text to translate</param>
        /// <param name="to">The language to translate to, null for no translation</param>
        /// <param name="from">The language of the text</param>
        /// <param name="context">Optional context that helps the translation</param>
        /// <param name="effort">The translation effort</param>
        /// <param name="retention">How long the translation should be cached</param>
        /// <param name="contentType">The type of content</param>
        /// <returns>The translated text, or the source text if translation isn't possible</returns>
        public static async Task<String> TranslateSafe(this ITranslator translator, String text, String to, String from = "en", String context = null, TranslationEffort effort = TranslationEffort.High, TranslationCacheRetention retention = TranslationCacheRetention.Long, TranslationContentTypes contentType = TranslationContentTypes.Text)
        {
            if (translator == null)
                return text;
            if (String.IsNullOrEmpty(text))
                return text;
            if (to == null)
                return text;
            if (to.FastEquals(from))
                return text;
            try
            {
                var l = text.Length;
                int start;
                for (start = 0; start < l; ++ start)
                {
                    if (!Char.IsWhiteSpace(text[start]))
                        break;
                }
                if (start >= l)
                    return text;
                int end = l;
                while (end > 0)
                {
                    --end;
                    if (!Char.IsWhiteSpace(text[end]))
                    {
                        ++end;
                        break;
                    }
                }
                var newText = await translator.TranslateOne(new TranslateRequest
                {
                    Context = context,
                    From = from,
                    To = to,
                    Text = text.Substring(start, end - start),
                    Effort = effort,
                    Retention = retention,
                    ContentType = contentType,
                }).ConfigureAwait(false);
                if (newText == null)
                    return text;
                newText = String.Concat(text.Substring(0, start), newText, text.Substring(end));
                return newText;
            }
            catch
            {
                return text;
            }
        }
    }


    

    /// <summary>
    /// Get managed messages (emails, SMS and texts) in the language of a session, auto translating (and caching) missing languages.
    /// </summary>
    public static class ManagedMessagesExt
    {

        static async Task DoEmail(ConcurrentDictionary<string, ManagedMailMessage> dest, String key, ITranslator tr, String defLang, String to, ManagedMailMessage text)
        {
            var h = text.IsHtml;
            var body = text.GetBody().Template;
            var x = new ManagedMailMessage(text.Vars)
            {
                IsHtml = h,
                Subject = await tr.TranslateSafe(text.GetSubject().Template, to, defLang, "This is an email subject line. Text between [ and ] are variables and should not be translated, examples: \"[Amount]\", \"[_Site]\" and \"[#Root]\".").ConfigureAwait(false),
                Body = await(h ? tr.TranslateSafeHtml(body, to, defLang, "This is an email body. Text between [ and ] are variables and should not be translated, examples: \"[Amount]\", \"[_Site]\" and \"[#Root]\".") : tr.TranslateSafe(body, to, defLang, "This is the email body. Text between [ and ] are variables and should not be translated, examples: \"[Amount]\", \"[_Site]\" and \"[#Root]\".")).ConfigureAwait(false),
            };
            dest[key] = x;
        }

        static async Task DoSMS(ConcurrentDictionary<string, ManagedTextMessage> dest, String key, ITranslator tr, String defLang, String to, ManagedTextMessage text)
        {
            var x = new ManagedTextMessage(text.Vars)
            {
                Body = await tr.TranslateSafe(text.GetBody().Template, to, defLang, "This is the text of a SMS, keep short. Text between [ and ] are variables and should not be translated, examples: \"[Amount]\", \"[_Site]\" and \"[#Root]\".").ConfigureAwait(false),
            };
            dest[key] = x;
        }

        static async Task DoText(ConcurrentDictionary<string, String> dest, String key, ITranslator tr, String defLang, String to, String text)
        {
            if (key[0] == '_')
            {
                var t = (await tr.TranslateSafe(key.Substring(1), to, defLang).ConfigureAwait(false)).Split(' ')[0];
                dest["_" + t] = text;
                return;
            }
            dest[key] = await tr.TranslateSafe(text, to, defLang).ConfigureAwait(false);
        }

        /// <summary>
        /// Get the messages in the language of the request's session, see <see cref="GetLang(ManagedMessages, String, ITranslator)"/>.
        /// </summary>
        /// <param name="msg">The messages</param>
        /// <param name="context">The request</param>
        /// <returns>The messages for the language</returns>
        public static Task<ManagedLanguageMessages> GetLang(this ManagedMessages msg, HttpServerRequest context)
            => GetLang(msg, context.Session.Language, context.Translator);

        /// <summary>
        /// Get the messages for a language. If the language isn't defined (a fallback is returned) and a translator is supplied,
        /// all emails and SMS of the fallback language are translated and the result is added to <paramref name="msg"/> for later use.
        /// </summary>
        /// <param name="msg">The messages</param>
        /// <param name="l">The language, null for "en"</param>
        /// <param name="tr">The translator, may be null</param>
        /// <returns>The messages for the language</returns>
        public static async Task<ManagedLanguageMessages> GetLang(this ManagedMessages msg, String l, ITranslator tr)
        {
            l = l ?? "en";
            var lang = msg.GetLang(l);
            if ((tr == null) || (!lang.IsFallback))
                return lang;
            //  Translate
            var defLang = lang.Language;
            var texts = new ConcurrentDictionary<string, ManagedTextMessage>(StringComparer.Ordinal);
            var mails = new ConcurrentDictionary<string, ManagedMailMessage>(StringComparer.Ordinal);
            List<Task> tasks = new List<Task>();
            foreach (var m in lang.AllMail)
                tasks.Add(DoEmail(mails, m.Key, tr, defLang, l, m.Value));
            foreach (var m in lang.AllTexts)
                tasks.Add(DoSMS(texts, m.Key, tr, defLang, l, m.Value));
            await Task.WhenAll(tasks).ConfigureAwait(false);
            lang = new ManagedLanguageMessages(l, texts.Freeze(), mails.Freeze());
            if (!msg.TryAddTranslatedLanguage(l, lang))
                lang = msg.GetLang(l);
            return lang;
        }

        /// <summary>
        /// Get the texts for a language. If the language isn't defined and a translator is supplied, the default texts are translated and the result is added to <paramref name="msg"/> for later use.
        /// Keys starting with '_' are translated themselves (first word only) while their values are kept.
        /// </summary>
        /// <param name="msg">The texts</param>
        /// <param name="l">The language, null for "en"</param>
        /// <param name="tr">The translator, may be null</param>
        /// <param name="onNew">Optional function called with a newly translated language before it's added</param>
        /// <returns>The texts for the language</returns>
        public static async Task<ManagedLanguageTexts> GetLang(this ManagedTexts msg, String l, ITranslator tr, Func<ManagedLanguageTexts, ITranslator, Task> onNew = null)
        {
            l = l ?? "en";
            var lang = msg.TryGetLang(l);
            if (lang != null)
                return lang;
            lang = msg.Default;
            if (tr == null)
                return lang;
            //  Translate
            var defLang = lang.Language;
            var texts = new ConcurrentDictionary<string, String>(StringComparer.Ordinal);
            List<Task> tasks = new List<Task>();
            foreach (var m in lang.AllTexts)
                tasks.Add(DoText(texts, m.Key, tr, defLang, l, m.Value));
            await Task.WhenAll(tasks).ConfigureAwait(false);
            lang = new ManagedLanguageTexts(l, String.Join('\n', texts.Select(x => String.Join(":", x.Key, x.Value))));
            if (onNew != null)
                await onNew(lang, tr).ConfigureAwait(false);
            if (!msg.TryAddTranslatedLanguage(l, lang))
                lang = msg.GetLang(l);
            return lang;
        }

    }



}
