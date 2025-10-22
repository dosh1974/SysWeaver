using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SysWeaver.Net;
using SysWeaver.Translation;

namespace SysWeaver
{

    public static class TranslatorExt
    {
        public static async Task<String> TranslateSafeHtml(this ITranslator translator, String htmlText, String to, String from = "en", String context = null, TranslationEffort effort = TranslationEffort.High, TranslationCacheRetention retention = TranslationCacheRetention.Long)
        {
            if (translator == null)
                return htmlText;
            if (to == null)
                return htmlText;
            if (to.FastEquals(from))
                return htmlText;
            try
            {
                var newText = await translator.TranslateOne(new TranslateRequest
                {
                    Context = "This text is HTML, any HTML elements, attributes etc shouldn't be translated, unless title, placeholder attributes.\nAnything between a '[' and a ']' is a variable, and must NOT be translated but kept, ex: \"[Header]\", \"[UserName]\".\n" + (context ?? ""),
                    From = from,
                    To = to,
                    Text = htmlText,
                    Effort = effort,
                    Retention = retention
                }).ConfigureAwait(false);
                return newText ?? htmlText;
            }
            catch
            {
                return htmlText;
            }
        }

        public static async Task<String> TranslateSafe(this ITranslator translator, String text, String to, String from = "en", String context = null, TranslationEffort effort = TranslationEffort.High, TranslationCacheRetention retention = TranslationCacheRetention.Long)
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
                var newText = await translator.TranslateOne(new TranslateRequest
                {
                    Context = context,
                    From = from,
                    To = to,
                    Text = text,
                    Effort = effort,
                    Retention = retention
                }).ConfigureAwait(false);
                return newText ?? text;
            }
            catch
            {
                return text;
            }
        }
    }


    

    public static class ManagedMessagesExt
    {

        static async Task DoEmail(ConcurrentDictionary<string, ManagedMailMessage> dest, String key, ITranslator tr, String defLang, String to, ManagedMailMessage text)
        {
            var h = text.IsHtml;
            var body = text.GetBody().Template;
            var x = new ManagedMailMessage
            {
                IsHtml = h,
                Subject = await tr.TranslateSafe(text.GetSubject().Template, to, defLang, "This is an email subject line").ConfigureAwait(false),
                Body = await(h ? tr.TranslateSafeHtml(body, to, defLang, "This is an email body") : tr.TranslateSafe(body, to, defLang, "This is the email body")).ConfigureAwait(false),
            };
            dest[key] = x;
        }

        static async Task DoSMS(ConcurrentDictionary<string, ManagedTextMessage> dest, String key, ITranslator tr, String defLang, String to, ManagedTextMessage text)
        {
            var x = new ManagedTextMessage
            {
                Body = await tr.TranslateSafe(text.GetBody().Template, to, defLang, "This is the text of a SMS, keep short").ConfigureAwait(false),
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

        public static Task<ManagedLanguageMessages> GetLang(this ManagedMessages msg, HttpServerRequest context)
            => GetLang(msg, context.Session.Language, context.Translator);

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
