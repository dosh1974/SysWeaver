using System;

namespace SysWeaver.Chat
{
    /// <summary>
    /// Used to create a new chat session
    /// </summary>
    public sealed class ChatCreateRequest
    {
        /// <summary>
        /// The provider to create a session for
        /// </summary>
        public String Provider;
        /// <summary>
        /// The provider specific type to create a session
        /// </summary>
        public String Type;
    }

}
