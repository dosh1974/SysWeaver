namespace SysWeaver.Chat
{
    /// <summary>
    /// Indicates what type the text in the message body is.
    /// All messages are encoded using UTF-8.
    /// </summary>
    public enum ChatMessageFormats
    {
        /// <summary>
        /// Plain text
        /// </summary>
        Text = 0,
        /// <summary>
        /// MarkDown text.
        /// </summary>
        MarkDown,
        /// <summary>
        /// HTML code.
        /// </summary>
        HTML,
    }
}
