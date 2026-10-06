using System;

namespace SysWeaver.Net
{
    /// <summary>
    /// Information about a supported language, with names localized to the language of the requesting session (see <see cref="HttpServerBase.GetLocalizedLanguages"/>).
    /// </summary>
    public sealed class LanguageInfo
    {
        /// <summary>
        /// The two letter ISO 639-1 language code of this language
        /// </summary>
        public String Iso;

        /// <summary>
        /// The official name of this language in the currently selected language
        /// </summary>
        public String Name;

        /// <summary>
        /// The official name of this language in the language itself
        /// </summary>
        public String LocalName;

        /// <summary>
        /// An optional comment in the currently selected language
        /// </summary>
        public String Comment;


        /// <summary>
        /// The official name of this language in english
        /// </summary>
        public String EnName;

        /// <summary>
        /// Create an empty instance (for serialization).
        /// </summary>
        public LanguageInfo()
        {
        }
        /// <summary>
        /// Create a language info.
        /// </summary>
        /// <param name="iso">The ISO 639-1 language code</param>
        /// <param name="enName">The English name of the language</param>
        /// <param name="name">The name of the language in the currently selected language</param>
        /// <param name="localName">The name of the language in the language itself</param>
        /// <param name="comment">An optional comment in the currently selected language</param>
        public LanguageInfo(string iso, string enName, string name, string localName, string comment)
        {
            Iso = iso;
            Name = name;
            EnName = enName;
            LocalName = localName;
            Comment = comment;
        }
    }

}
