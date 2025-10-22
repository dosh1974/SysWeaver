using System;
using SysWeaver.Net;

namespace SysWeaver.Chat
{

    public interface IChatController
    {
        /// <summary>
        /// Post a message to the given chat session
        /// </summary>
        /// <param name="providerChatId">The unique id of the chat session, as returned by the CreateNewChat method (or any static chat "channels" available)</param>
        /// <param name="message">The message to send to clients listening to this chat session</param>
        /// <param name="session">The current session</param>
        /// <param name="scope">The scope of this chat</param>
        void PostMessage(String providerChatId, ChatMessage message, HttpSession session, ChatScopes scope);

        /// <summary>
        /// Replace / update and already existing message in the given chat session
        /// </summary>
        /// <param name="providerChatId">The unique id of the chat session, as returned by the CreateNewChat method (or any static chat "channels" available)</param>
        /// <param name="message">The message to update/replace to clients listening to this chat session (the message Id must already exist)</param>
        /// <param name="session">The current session</param>
        /// <param name="scope">The scope of this chat</param>
        void ReplaceMessage(String providerChatId, ChatMessage message, HttpSession session, ChatScopes scope);

        /// <summary>
        /// Remove an existing message in the given chat session
        /// </summary>
        /// <param name="providerChatId">The unique id of the chat session, as returned by the CreateNewChat method (or any static chat "channels" available)</param>
        /// <param name="messageId">The id of a message that already exist</param>
        /// <param name="session">The current session</param>
        /// <param name="scope">The scope of this chat</param>
        void RemoveMessage(String providerChatId, long messageId, HttpSession session, ChatScopes scope);

        /// <summary>
        /// Remoe all existing messages in the given chat session
        /// </summary>
        /// <param name="providerChatId">The unique id of the chat session, as returned by the CreateNewChat method (or any static chat "channels" available)</param>
        /// <param name="session">The current session</param>
        /// <param name="scope">The scope of this chat</param>
        void ClearAllMessages(String providerChatId, HttpSession session, ChatScopes scope);

    }




}
