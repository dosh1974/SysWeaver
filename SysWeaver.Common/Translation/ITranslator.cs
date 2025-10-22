using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SysWeaver.Translation
{


    public interface ITranslator : IDisposable
    {
        /// <summary>
        /// Translate some text to one or more languages
        /// </summary>
        /// <param name="request">Paramaters</param>
        /// <returns>Translations in the same order as specified in the parameters</returns>
        Task<string[]> Translate(TranslateRequest request);

        /// <summary>
        /// Translate multiple texts to one or more languages
        /// </summary>
        /// <param name="request">Paramaters</param>
        /// <returns>Translations in the same order as specified in the parameters</returns>
        Task<string[]> TranslateMultiple(TranslateMultipleRequest request);

        /// <summary>
        /// Translate some text to a new language
        /// </summary>
        /// <param name="request">Paramaters</param>
        /// <returns>Translated text</returns>
        Task<string> TranslateOne(TranslateRequest request);

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
        /// <param name="from"></param>
        /// <returns></returns>
        Task<String> CanFrom(String from);

        /// <summary>
        /// Returns a formatted to language if it's valid, else null
        /// </summary>
        /// <param name="to"></param>
        /// <returns></returns>
        Task<String> CanTo(String to);
    }

}
