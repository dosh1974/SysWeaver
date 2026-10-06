using System;

namespace SysWeaver.LanguageIdentifier
{
    /// <summary>
    /// A language identified by an <see cref="ILanguageIdentifier"/> and the confidence of the identification
    /// </summary>
    public sealed class IdentifiedLanguage
    {
#if DEBUG
        public override string ToString() => String.Concat(Language, ": ", Confidence);

#endif//DEBUG

        /// <summary>
        /// The two letter ISO 639-1 language code
        /// </summary>
        public String Language;

        /// <summary>
        /// The confidence of the identification, higher is more confident (typically [0, 1])
        /// </summary>
        public double Confidence;

        /// <summary>
        /// Create an empty instance (for serialization)
        /// </summary>
        public IdentifiedLanguage()
        {
        }

        /// <summary>
        /// Create an instance
        /// </summary>
        /// <param name="language">The two letter ISO 639-1 language code</param>
        /// <param name="confidence">The confidence of the identification</param>
        public IdentifiedLanguage(string language, double confidence)
        {
            Language = language;
            Confidence = confidence;
        }
    }

}