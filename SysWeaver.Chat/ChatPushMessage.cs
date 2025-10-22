using System;

namespace SysWeaver.Chat
{
    /// <summary>
    /// A push message with chat updates
    /// </summary>
    public sealed class ChatPushMessage : PushMessage
    {
        public ChatPushMessage(String type, ChatMessage chat)
            : base(type)
        {
            Chat = chat;
        }
        /// <summary>
        /// The chat message that was added or updated
        /// </summary>
        public ChatMessage Chat;
    }

}
