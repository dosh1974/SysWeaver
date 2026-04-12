using System;
using SysWeaver.Translation;

namespace SysWeaver.MicroService
{
    public sealed class Translation
    {

        /// <summary>
        /// True if this translation was done manually
        /// </summary>
        public bool IsManual;

        /// <summary>
        /// Time stamp when translation was performed
        /// </summary>
        public DateTime Time;

        /// <summary>
        /// The language of the input text
        /// </summary>
        public String From;

        /// <summary>
        /// The language name of the input text
        /// </summary>
        public String FromName;

        /// <summary>
        /// The text to translate.
        /// Text is truncated to at most 768 chars.
        /// </summary>
        public String Text;

        /// <summary>
        /// The language to translate to
        /// </summary>
        public String To;

        /// <summary>
        /// The language name to translate to
        /// </summary>
        public String ToName;

        /// <summary>
        /// The context used for the translation.
        /// Text is truncated to at most 768 chars.
        /// </summary>
        public String Context;

        /// <summary>
        /// The translated text.
        /// </summary>
        public String Translated;

        /// <summary>
        /// The type of content
        /// </summary>
        public TranslationContentTypes ContentType;

        /// <summary>
        /// The user that manually changed this
        /// </summary>
        public String UserName;

        /// <summary>
        /// The name of the serive / user that requested this translation
        /// </summary>
        public String ServiceName;

    }




}
