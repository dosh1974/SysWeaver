using System;

namespace SysWeaver.Translation
{

    /// <summary>
    /// Translates a single text 
    /// </summary>
    public sealed class TranslateRequest
    {
        /// <summary>
        /// The two letter ISO-639-1 language code of the source language (the supplied text).
        /// "*" can be used to let the translator identify the source language.
        /// </summary>
        [EditMin(1)]
        public String From { get; set; }

        /// <summary>
        /// The two letter ISO-639-1 language code of the target language (with an optional two letter ISO-3166-a2 country code appended with a hyphen, ex: "zw-CH").
        /// Multiple targets can be set by using a comma separation (not possible for the *One methods).
        /// </summary>
        [EditMin(1)]
        public String To { get; set; }

        /// <summary>
        /// The text to translate.
        /// If it's starting with "{MD}" the text is assumed to be in the Mark Down format.
        /// </summary>
        [EditMin(1)]
        [EditMultiline]
        public String Text { get; set; }

        /// <summary>
        /// An optional context that describes in what situation this text is being used
        /// </summary>
        [EditMultiline]
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
