using System;

namespace SysWeaver.Translation
{
    /// <summary>
    /// Provides a translator and a target language, implemented by HttpServerRequest (using the session language).
    /// </summary>
    public interface ITranslationContext
    {
        /// <summary>
        /// The translator to use, may be null if no translator is available
        /// </summary>
        ITranslator Translator { get; }
        /// <summary>
        /// The language to translate to (ISO code), may be null
        /// </summary>
        String Language { get; }
    }



}
