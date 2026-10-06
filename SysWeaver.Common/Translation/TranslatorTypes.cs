namespace SysWeaver.Translation
{
    /// <summary>
    /// The type of text of an auto translated member, see <see cref="AutoTranslateTypeAttribute"/>
    /// </summary>
    public enum TranslatorTypes
    {
        /// <summary>
        /// The text is pure text
        /// </summary>
        Text = 0,
        /// <summary>
        /// The text is using MarkDown syntax
        /// </summary>
        MD = 1,
        /// <summary>
        /// The text is html code
        /// </summary>
        Html = 2,
    }



}
