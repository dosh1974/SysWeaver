using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SysWeaver.Translation
{


    /// <summary>
    /// Represents an object that can translate texts between languages (typically a translator service, possibly with caching).
    /// </summary>
    public interface ITranslator : IDisposable
    {
        /// <summary>
        /// Translate some text to one or more languages
        /// </summary>
        /// <param name="request">Parameters</param>
        /// <returns>Translations in the same order as specified in the parameters</returns>
        Task<string[]> Translate(TranslateRequest request);

        /// <summary>
        /// Translate multiple texts to one or more languages
        /// </summary>
        /// <param name="request">Parameters</param>
        /// <returns>Translations in the same order as specified in the parameters, all texts for the first target language followed by all texts for the next target language and so on</returns>
        Task<string[]> TranslateMultiple(TranslateMultipleRequest request);

        /// <summary>
        /// Translate some text to a new language
        /// </summary>
        /// <param name="request">Parameters (only a single target language is allowed)</param>
        /// <returns>Translated text</returns>
        ValueTask<string> TranslateOne(TranslateRequest request);

        /// <summary>
        /// Return a list of supported source languages
        /// </summary>
        /// <returns>A list of supported source languages</returns>
        Task<IReadOnlyList<String>> GetSupportedSourceLanguages();

        /// <summary>
        /// Return a list of supported target languages
        /// </summary>
        /// <returns>A list of supported target languages</returns>
        Task<IReadOnlyList<String>> GetSupportedTargetLanguages();


        /// <summary>
        /// Returns a formatted from language if it's valid, else null
        /// </summary>
        /// <param name="from">The source language code to check</param>
        /// <returns>The normalized language code, or null if the language isn't supported as a source language</returns>
        Task<String> CanFrom(String from);

        /// <summary>
        /// Returns a formatted to language if it's valid, else null
        /// </summary>
        /// <param name="to">The target language code to check</param>
        /// <returns>The normalized language code, or null if the language isn't supported as a target language</returns>
        Task<String> CanTo(String to);
    }

}
