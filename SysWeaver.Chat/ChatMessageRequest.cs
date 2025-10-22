using System;

namespace SysWeaver.Chat
{
    /// <summary>
    /// A request to post a new message to the given chat session
    /// </summary>
    public sealed class ChatMessageRequest : ChatBaseRequest
    {
        /// <summary>
        /// The chat message to post
        /// </summary>
        public ChatMessageBody Body;

    }
}
