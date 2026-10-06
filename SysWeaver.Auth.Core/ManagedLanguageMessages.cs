using System;
using System.Collections.Generic;

namespace SysWeaver
{
    /// <summary>
    /// The localized text and mail message templates of a single language, created by <see cref="ManagedMessages"/>.
    /// </summary>
    public sealed class ManagedLanguageMessages
    {
        /// <inheritdoc/>
        public override string ToString() => Language;

        /// <summary>
        /// The language (as named by the source folder), ex: "en-US"
        /// </summary>
        public readonly String Language;


        /// <summary>
        /// True if this is used as a fallback (the default language returned when the requested language isn't available)
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


        /// <summary>
        /// All text messages, key is the lower cased message name.
        /// </summary>
        public IEnumerable<KeyValuePair<String, ManagedTextMessage>> AllTexts => Texts;
        /// <summary>
        /// All mail messages, key is the lower cased message name.
        /// </summary>
        public IEnumerable<KeyValuePair<String, ManagedMailMessage>> AllMail => Mails;

        /// <summary>
        /// Get the mail message for a given key.
        /// </summary>
        /// <param name="key">The key (one of the names supplied to <see cref="ManagedMessages"/>, including any leading '*', case insensitive)</param>
        /// <returns>null if not found, else the mail message template</returns>
        public ManagedMailMessage GetMail(String key)
            => Mails.TryGetValue(key.FastToLower(), out var v) ? v : null;

        /// <summary>
        /// Get the text message for a given key.
        /// </summary>
        /// <param name="key">The key (one of the names supplied to <see cref="ManagedMessages"/>, including any leading '*', case insensitive)</param>
        /// <returns>null if not found, else the text message template</returns>
        public ManagedTextMessage GetText(String key)
            => Texts.TryGetValue(key.FastToLower(), out var v) ? v : null;


        /// <summary>
        /// Create the messages of a language.
        /// </summary>
        /// <param name="language">The language</param>
        /// <param name="texts">The text messages, keys must be lower case</param>
        /// <param name="mails">The mail messages, keys must be lower case</param>
        public ManagedLanguageMessages(String language, IReadOnlyDictionary<string, ManagedTextMessage> texts, IReadOnlyDictionary<string, ManagedMailMessage> mails)
        {
            Language = language;
            Texts = texts;
            Mails = mails;
        }

        

        /// <summary>
        /// Create a copy (sharing the messages) with a different <see cref="IsFallback"/> value.
        /// </summary>
        /// <param name="copy">The messages to copy</param>
        /// <param name="isFallback">True if the copy is used as a fallback</param>
        internal ManagedLanguageMessages(ManagedLanguageMessages copy, bool isFallback = true)
        {
            IsFallback = isFallback;
            Language = copy.Language;
            Texts = copy.Texts;
            Mails = copy.Mails;
        }


    }


}
