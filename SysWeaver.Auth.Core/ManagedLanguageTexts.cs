using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SysWeaver.Translation;

namespace SysWeaver
{
    public sealed class ManagedLanguageTexts : ManagedTextLookup
    {
        public override string ToString() => Language;

        /// <summary>
        /// The language
        /// </summary>
        public readonly String Language;

        public readonly bool IsFallback;

        public ManagedLanguageTexts(String language, String filename) : base(filename)
        {
            Language = language;
        }


        internal ManagedLanguageTexts(ManagedLanguageTexts texts, bool isFallback = true) : base(texts)
        {
            Language = texts.Language;
            IsFallback = isFallback;
        }

    }
    
}
