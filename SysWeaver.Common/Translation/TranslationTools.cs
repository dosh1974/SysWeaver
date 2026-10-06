using System;

namespace SysWeaver.Translation
{
    /// <summary>
    /// Translation constants
    /// </summary>
    public static class TranslationTools
    {
        /// <summary>
        /// Any string that starts with this sequence will NOT be translated (the translators return the text with the prefix removed).
        /// </summary>
        public const String NoTranslatePrefix = "_ä_";

        /// <summary>
        /// Length of the <see cref="NoTranslatePrefix"/>
        /// </summary>
        public const int NoTranslatePrefixLength = 3;

    }



}
