using System;

namespace SysWeaver.Translation
{
    public interface ITranslationContext
    {
        ITranslator Translator { get; }
        String Language { get; }
    }



}
