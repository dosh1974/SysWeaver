using System;
using SysWeaver.Translation;

namespace SysWeaver
{
    /// <summary>
    /// Put this attribute on a member to allow it to be automatically translated when returned in an API call (if auto-translation is enabled etc).
    /// By default the translation context will be created using the code summary of the member.
    /// </summary>
    /// <remarks>
    /// Used by the table data system (SysWeaver.TableData) to translate string members of table rows and other returned objects to the language of the request.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class AutoTranslateAttribute : Attribute
    {
        /// <summary>
        /// Put this attribute on a member to allow it to be automatically translated when returned in an API call (if auto-translation is enabled etc).
        /// </summary>
        /// <param name="fromLanguage">The source language (ISO code), null for english ("en")</param>
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

        /// <summary>
        /// The source language (ISO code), null for english ("en")
        /// </summary>
        public readonly String FromLanguage;
        /// <summary>
        /// True if the code summary of the member should NOT be included in the context
        /// </summary>
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
        /// <param name="memberNames">List of type members whose values will be passed in as arguments
        /// If any member value is null or empty, the whole context string is ignored.
        /// </param>
        public AutoTranslateContextAttribute(String contextText, params String[] memberNames)
        {
            ContextText = contextText;
            MemberNames = memberNames;
        }

        /// <summary>
        /// Put this attribute on an auto translated member to add additional context when auto translating this member.
        /// </summary>
        /// <param name="contextText">The additional context to add, see <see cref="AutoTranslateContextAttribute(String, String[])"/></param>
        /// <param name="order">The order of this context relative to other contexts on the same member (low to high)</param>
        /// <param name="memberNames">List of type members whose values will be passed in as arguments</param>
        public AutoTranslateContextAttribute(String contextText, double order, params String[] memberNames)
        {
            Order = order;
            ContextText = contextText;
            MemberNames = memberNames;
        }

        /// <summary>
        /// Put this attribute on an auto translated member to add additional context when auto translating this member.
        /// </summary>
        /// <param name="order">The order of this context relative to other contexts on the same member (low to high)</param>
        /// <param name="contextText">The additional context to add, see <see cref="AutoTranslateContextAttribute(String, String[])"/></param>
        /// <param name="memberNames">List of type members whose values will be passed in as arguments</param>
        public AutoTranslateContextAttribute(double order, String contextText, params String[] memberNames)
        {
            Order = order;
            ContextText = contextText;
            MemberNames = memberNames;
        }

        /// <summary>
        /// The order of this context relative to other contexts on the same member (low to high)
        /// </summary>
        public readonly double Order;
        /// <summary>
        /// The context text, a <see cref="String.Format(String, Object[])"/> format if <see cref="MemberNames"/> are used
        /// </summary>
        public readonly String ContextText;
        /// <summary>
        /// The names of the members whose values are used as format arguments
        /// </summary>
        public readonly String[] MemberNames;
    }



    /// <summary>
    /// Put this attribute on a member to indicate that the text is of a specific type that needs to be handled differently.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class AutoTranslateTypeAttribute : Attribute
    {
        /// <summary>
        /// Put this attribute on a member to indicate that the text is of a specific type that needs to be handled differently.
        /// </summary>
        /// <param name="type">The type of text that this member should be treated as</param>
        public AutoTranslateTypeAttribute(TranslatorTypes type = TranslatorTypes.Text)
        {
            Type = type;
        }

        /// <summary>
        /// The type of text that this member should be treated as
        /// </summary>
        public readonly TranslatorTypes Type;
    }



    /// <summary>
    /// Put this attribute on a member to specify a property that returns the language to translate from.
    /// By default the language specified in the <see cref="AutoTranslateAttribute"/> is used.
    /// </summary>
    /// <remarks>
    /// Building the translator for the type throws if the declaring type doesn't contain the member.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class AutoTranslateDynLanguageAttribute : Attribute
    {
        /// <summary>
        /// Put this attribute on a member to specify a property that returns the language to translate from.
        /// By default the language specified in the <see cref="AutoTranslateAttribute"/> is used.
        /// </summary>
        /// <param name="memberName">The name of a member in the declaring type that is a String with the language code</param>
        public AutoTranslateDynLanguageAttribute(String memberName)
        {
            MemberName = memberName;
        }

        /// <summary>
        /// The name of a member in the declaring type that is a String with the language code
        /// </summary>
        public readonly String MemberName;
    }

}
