using System;

namespace SysWeaver.Chat
{
    /// <summary>
    /// The base for any request that requires a chat id
    /// </summary>
    public class ChatBaseRequest
    {
        /// <summary>
        /// The id of the chat session to target
        /// </summary>
        public String ChatId;
    }

}
