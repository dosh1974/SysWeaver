namespace SysWeaver.Chat
{
    /// <summary>
    /// A request that refers to one message of a chat.
    /// </summary>
    public class ChatMessageIdRequest : ChatBaseRequest
    {
        /// <summary>
        /// The id of the message
        /// </summary>
        public long MessageId;
    }

}
