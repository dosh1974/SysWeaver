using System;

namespace SysWeaver.Net
{
    public sealed class LanguageInfo
    {
        /// <summary>
        /// Language code
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
        /// An optional comment  in the currently selected language
        /// </summary>
        public String Comment;


        /// <summary>
        /// The official name of this language in english
        /// </summary>
        public String EnName;

        public LanguageInfo()
        {
        }
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
