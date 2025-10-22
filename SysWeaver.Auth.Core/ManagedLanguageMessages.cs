using System;
using System.Collections.Generic;

namespace SysWeaver
{
    public sealed class ManagedLanguageMessages
    {
        public override string ToString() => Language;

        /// <summary>
        /// The language
        /// </summary>
        public readonly String Language;


        /// <summary>
        /// true if this is used as a fallback
        /// </summary>
        public readonly bool IsFallback;

        /// <summary>
        /// Text messages
        /// </summary>
        readonly IReadOnlyDictionary<String, ManagedTextMessage> Texts;

        /// <summary>
        /// Mail messages
        /// </summary>
        readonly IReadOnlyDictionary<String, ManagedMailMessage> Mails;


        public IEnumerable<KeyValuePair<String, ManagedTextMessage>> AllTexts => Texts;
        public IEnumerable<KeyValuePair<String, ManagedMailMessage>> AllMail => Mails;

        /// <summary>
        /// Get the mail message for a given key.
        /// </summary>
        /// <param name="key">The key (one of the names supplied in the constructor)</param>
        /// <returns>null if not found, else the mail message template</returns>
        public ManagedMailMessage GetMail(String key)
            => Mails.TryGetValue(key.FastToLower(), out var v) ? v : null;

        /// <summary>
        /// Get the text message for a given key.
        /// </summary>
        /// <param name="key">The key (one of the names supplied in the constructor)</param>
        /// <returns>null if not found, else the text message template</returns>
        public ManagedTextMessage GetText(String key)
            => Texts.TryGetValue(key.FastToLower(), out var v) ? v : null;


        public ManagedLanguageMessages(String language, IReadOnlyDictionary<string, ManagedTextMessage> texts, IReadOnlyDictionary<string, ManagedMailMessage> mails)
        {
            Language = language;
            Texts = texts;
            Mails = mails;
        }

        internal ManagedLanguageMessages(ManagedLanguageMessages copy, bool isFallback = true)
        {
            IsFallback = isFallback;
            Language = copy.Language;
            Texts = copy.Texts;
            Mails = copy.Mails;
        }


    }


}
