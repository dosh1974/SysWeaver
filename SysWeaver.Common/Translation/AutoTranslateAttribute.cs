using System;
using SysWeaver.Translation;

namespace SysWeaver
{
    /// <summary>
    /// Put this attribute on a member to allow it to be automatically translated when returned in an API call (if auto-translation is enabled etc).
    /// By default the translation context will be created using the code summary of the member.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class AutoTranslateAttribute : Attribute
    {
        /// <summary>
        /// Put this attribute on a member to allow it to be automatically translated when returned in an API call (if auto-translation is enabled etc).
        /// </summary>
        /// <param name="fromLanguage">The source language (ISO code) if not english</param>
        /// <param name="contextFromDesc">If true, the code summary of the member will be included in the context</param>
        public AutoTranslateAttribute(String fromLanguage = null, bool contextFromDesc = true)
        {
            FromLanguage = fromLanguage;
            NoContext = !contextFromDesc;
        }

        /// <summary>
        /// Put this attribute on a member to allow it to be automatically translated when returned in an API call (if auto-translation is enabled etc).
        /// </summary>
        /// <param name="contextFromDesc">If true, the code summary of the member will be included in the context</param>
        public AutoTranslateAttribute(bool contextFromDesc)
        {
            NoContext = !contextFromDesc;
        }

        public readonly String FromLanguage;
        public readonly bool NoContext;
    }


    /// <summary>
    /// Put this attribute on an auto translated member to add additional context when auto translating this member.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
    public sealed class AutoTranslateContextAttribute : Attribute
    {
        /// <summary>
        /// Put this attribute on an auto translated member to add additional context when auto translating this member.
        /// </summary>
        /// <param name="contextText">The additional context to add when auto translating this member.
        /// Then final text will use String.Format(contextText, ...);
        /// ... = The values of the members passed in as arguments.</param>
        /// <param name="memberNames">List of type members who's values will be passed in as arguments
        /// If any member value is null or empty, the whole context string is ignored.
        /// </param>
        public AutoTranslateContextAttribute(String contextText, params String[] memberNames)
        {
            ContextText = contextText;
            MemberNames = memberNames;
        }
        public readonly String ContextText;
        public readonly String[] MemberNames;
    }



    /// <summary>
    /// Put this attribute on a member to indicate that the text is of a specfic type that needs to be handles differently.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class AutoTranslateTypeAttribute : Attribute
    {
        /// <summary>
        /// Put this attribute on a member to indicate that the text is of a specfic type that needs to be handles differently.
        /// </summary>
        /// <param name="type">The type of text that this member should be treated as</param>
        public AutoTranslateTypeAttribute(TranslatorTypes type = TranslatorTypes.Text)
        {
            Type = type;
        }

        public readonly TranslatorTypes Type;
    }



    /// <summary>
    /// Put this attribute on a member to specify a property that returns the language to translate from.
    /// By default the language specified in the AutoTranslateContextAttribute is used. 
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class AutoTranslateDynLanguageAttribute : Attribute
    {
        /// <summary>
        /// Put this attribute on a member to specify a property that returns the language to translate from.
        /// By default the language specified in the AutoTranslateContextAttribute is used. 
        /// </summary>
        /// <param name="memberName">The name of a member in the declaring type that is a String with the language code</param>
        public AutoTranslateDynLanguageAttribute(String memberName)
        {
            MemberName = memberName;
        }

        public readonly String MemberName;
    }

}
