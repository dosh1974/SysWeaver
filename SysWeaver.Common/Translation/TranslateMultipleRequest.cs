using System;

namespace SysWeaver.Translation
{
    /// <summary>
    /// Translates multiple texts
    /// </summary>
    public sealed class TranslateMultipleRequest
    {
        /// <summary>
        /// The two letter ISO-639-1 language code of the source language (the supplied text).
        /// "*" can be used to let the translator identify the source language.
        /// </summary>
        public String From { get; set; }

        /// <summary>
        /// The two letter ISO-639-1 language code of the target language (with an optional two letter ISO-3166-a2 country code appended with a hyphen, ex: "zw-CH").
        /// Multiple targets can be set by using a comma separation (not possible for the *One methods).
        /// </summary>
        public String To { get; set; }

        /// <summary>
        /// The texts to translate.
        /// If any text is starting with "{MD}" that text is assumed to be in the Mark Down format (and returned as such).
        /// </summary>
        public String[] Texts { get; set; }

        /// <summary>
        /// An optional context that describes in what situation the texts are being used
        /// </summary>
        public String Context { get; set; }

        /// <summary>
        /// The effort to use when translating
        /// </summary>
        public TranslationEffort Effort { get; set; } = TranslationEffort.Medium;

        /// <summary>
        /// The duration to cache the translation
        /// </summary>
        public TranslationCacheRetention Retention { get; set; } = TranslationCacheRetention.Medium;

    }


}
