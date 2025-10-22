using System;

namespace SysWeaver.LanguageIdentifier
{
    public sealed class IdentifiedLanguage
    {
#if DEBUG
        public override string ToString() => String.Concat(Language, ": ", Confidence);

#endif//DEBUG

        public String Language;

        public double Confidence;

        public IdentifiedLanguage()
        {
        }

        public IdentifiedLanguage(string language, double confidence)
        {
            Language = language;
            Confidence = confidence;
        }
    }

}