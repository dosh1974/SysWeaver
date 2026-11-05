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
        /// Only the server application may send HTML messages (clients posting HTML to <see cref="ChatService.UserMessage"/> are refused with 400).
        /// The web front end still sanitizes it (an element / attribute allow list) before showing it.
        /// </summary>
        HTML,
    }
}
