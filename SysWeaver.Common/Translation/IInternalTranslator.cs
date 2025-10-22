using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SysWeaver.Translation
{
    public interface IInternalTranslator : ITranslator
    {

        /// <summary>
        /// Translate some text to one or more languages
        /// </summary>
        /// <param name="text">The text to translate</param>
        /// <param name="to">The two letter ISO-639-1 language code of the target language (with an optional two letter ISO-3166-a2 country code appended with a hyphen, ex: "zw-CH").
        /// Multiple targets can be set by using a comma separation.</param>
        /// <param name="from">The two letter ISO-639-1 language code of the source language (the supplied text).
        /// "*" can be used to let the translator identify the source language.</param>
        /// <param name="context">An optional context that describes in what situation this text is being used</param>
        /// <param name="effort">The effort to use when translating</param>
        /// <param name="retention">The duration to cache the translation</param>
        /// <returns>Translations in the same order as specified in the parameters</returns>
        Task<string[]> Translate(string text, string to, string from = "en", String context = null, TranslationEffort effort = TranslationEffort.Medium, TranslationCacheRetention retention = TranslationCacheRetention.Medium);


        /// <summary>
        /// Translate multiple texts to one or more languages
        /// </summary>
        /// <param name="texts">The texts to translate</param>
        /// <param name="to">The two letter ISO-639-1 language code of the target language (with an optional two letter ISO-3166-a2 country code appended with a hyphen, ex: "zw-CH").
        /// Multiple targets can be set by using a comma separation.</param>
        /// <param name="from">The two letter ISO-639-1 language code of the source language (the supplied text).
        /// "*" can be used to let the translator identify the source language.</param>
        /// <param name="context">An optional context that describes in what situation that the texts are being used</param>
        /// <param name="effort">The effort to use when translating</param>
        /// <param name="retention">The duration to cache the translation</param>
        /// <returns>Translations in the same order as specified in the parameters</returns>
        Task<string[]> TranslateMultiple(string[] texts, string to, string from = "en", String context = null, TranslationEffort effort = TranslationEffort.Medium, TranslationCacheRetention retention = TranslationCacheRetention.Medium);


        /// <summary>
        /// Translate some text to a new language
        /// </summary>
        /// <param name="text">The text to translate</param>
        /// <param name="to">The two letter ISO-639-1 language code of the target language (with an optional two letter ISO-3166-a2 country code appended with a hyphen, ex: "zw-CH").</param>
        /// <param name="from">The two letter ISO-639-1 language code of the source language (the supplied text).
        /// "*" can be used to let the translator identify the source language.</param>
        /// <param name="context">An optional context that describes in what situation this text is being used</param>
        /// <param name="effort">The effort to use when translating</param>
        /// <param name="retention">The duration to cache the translation</param>
        /// <returns>Translated text</returns>
        Task<string> TranslateOne(string text, string to, string from = "en", String context = null, TranslationEffort effort = TranslationEffort.Medium, TranslationCacheRetention retention = TranslationCacheRetention.Medium);


        /// <summary>
        /// Perform a request bypassing any caches, you probably shouldn't use this!
        /// </summary>
        /// <param name="from">The two letter ISO-639-1 language code of the source language (the supplied text).
        /// "*" can be used to let the translator identify the source language.</param>
        /// <param name="to">The two letter ISO-639-1 language code of the target language (with an optional two letter ISO-3166-a2 country code appended with a hyphen, ex: "zw-CH").</param>
        /// <param name="text">The text to translate</param>
        /// <param name="context">An optional context that describes in what situation this text is being used</param>
        /// <param name="effort">The effort to use when translating</param>
        /// <param name="retention">The duration to cache the translation</param>
        /// <returns></returns>
        Task<String> RequestOne(String from, String to, String text, String context = null, TranslationEffort effort = TranslationEffort.Medium, TranslationCacheRetention retention = TranslationCacheRetention.Medium);

        /// <summary>
        /// Return a list of supported source languages
        /// </summary>
        /// <returns>A list of supported source languages</returns>
        IReadOnlyList<String> SupportedSourceLanguages();

        /// <summary>
        /// Return a list of supported target languages
        /// </summary>
        /// <returns>A list of supported target languages</returns>
        IReadOnlyList<String> SupportedTargetLanguages();


        /// <summary>
        /// Returns a formatted from language if it's valid, else null
        /// </summary>
        /// <param name="from"></param>
        /// <returns></returns>
        String CanTranslateFrom(String from);

        /// <summary>
        /// Returns a formatted to language if it's valid, else null
        /// </summary>
        /// <param name="to"></param>
        /// <returns></returns>
        String CanTranslateTo(String to);
    }



}
