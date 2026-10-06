using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SysWeaver.Translation;

namespace SysWeaver
{
    /// <summary>
    /// The localized text templates of a single language, created by <see cref="ManagedTexts"/>.
    /// </summary>
    public sealed class ManagedLanguageTexts : ManagedTextLookup
    {
        /// <inheritdoc/>
        public override string ToString() => Language;

        /// <summary>
        /// The language, ex: "en-US"
        /// </summary>
        public readonly String Language;

        /// <summary>
        /// True if this is used as a fallback (the default language returned when the requested language isn't available)
        /// </summary>
        public readonly bool IsFallback;

        /// <summary>
        /// Create the texts of a language, the texts are loaded immediately.
        /// </summary>
        /// <param name="language">The language</param>
        /// <param name="filename">A file name, embedded resource name or the actual "key: text" lines, see <see cref="ManagedTextLookup.Text"/></param>
        public ManagedLanguageTexts(String language, String filename) : base(filename)
        {
            Language = language;
        }


        /// <summary>
        /// Create a copy (sharing the templates) with a different <see cref="IsFallback"/> value.
        /// </summary>
        /// <param name="texts">The texts to copy</param>
        /// <param name="isFallback">True if the copy is used as a fallback</param>
        internal ManagedLanguageTexts(ManagedLanguageTexts texts, bool isFallback = true) : base(texts)
        {
            Language = texts.Language;
            IsFallback = isFallback;
        }

    }
    
}
