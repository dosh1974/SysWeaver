using System;
using System.Net;
using System.Threading.Tasks;
using SysWeaver.Net;

namespace SysWeaver.Chat
{

    /// <summary>
    /// Implementations of this interface may be used by the chat service to provide a front end for a chat
    /// </summary>
    public interface IChatProvider
    {
        /// <summary>
        /// Unique name for this chat provider
        /// </summary>
        String Name { get; }

        /// <summary>
        /// If the provider supports user creation of chat sessions, implement this function, return null if not supported
        /// </summary>
        /// <param name="type">Type of that the user wants to create</param>
        /// <param name="request">The http request of the callee</param>
        /// <returns>A unique id for the created chat session</returns>
        Task<String> CreateNewChat(String type, HttpServerRequest request);

        /// <summary>
        /// If the provider support that a user clears the chat (starts over), implement this function, else return false.
        /// </summary>
        /// <param name="providerChatId">The unique id of the chat session, as returned by the CreateNewChat method (or any static chat "channels" available)</param>
        /// <param name="request">The http request of the callee</param>
        /// <returns>True if chat was cleared, else false</returns>
        Task<bool> Clear(String providerChatId, HttpServerRequest request);

        /// <summary>
        /// Delete a chat message (must be allowed to do so)
        /// </summary>
        /// <param name="providerChatId">The unique id of the chat session, as returned by the CreateNewChat method (or any static chat "channels" available)</param>
        /// <param name="messageId">The id of the message to delete</param>
        /// <param name="request">The http request of the callee</param>
        /// <returns>True if message was deleted</returns>
        Task<bool> RemoveMessage(String providerChatId, long messageId, HttpServerRequest request);

        /// <summary>
        /// Get the id of the last (current) message for the given chat session (id's should increment by one for every new message in a chat session).
        /// The first message should have id one.
        /// </summary>
        /// <param name="providerChatId">The unique id of the chat session, as returned by the CreateNewChat method (or any static chat "channels" available)</param>
        /// <param name="request">The http request of the callee</param>
        /// <returns>The current id or zero if no messages exist in the session</returns>
        Task<long> GetCurrentId(String providerChatId, HttpServerRequest request);

        /// <summary>
        /// Get messages from the given chat session and chat parameters
        /// </summary>
        /// <param name="providerChatId">The unique id of the chat session, as returned by the CreateNewChat method (or any static chat "channels" available)</param>
        /// <param name="request">The http request of the callee</param>
        /// <param name="pivotId">The first message to retrieve (inclusive), if maxCount is zero or negative this is the last message to retrieve (exclusive).
        /// If equal or less than zero, get messages from the end</param>
        /// <param name="maxCount">The maximum number of messages to retrieve, if negative retrieve message up until the pivot message id</param>
        /// <returns>An array of chat messages, can be empty or null if no new data exists</returns>
        Task<ChatJoinResponse> Join(String providerChatId, HttpServerRequest request, long pivotId, int maxCount);

        /// <summary>
        /// Get messages from the given chat session.
        /// </summary>
        /// <param name="providerChatId">The unique id of the chat session, as returned by the CreateNewChat method (or any static chat "channels" available)</param>
        /// <param name="request">The http request of the callee</param>
        /// <param name="pivotId">The first message to retrieve (inclusive), if maxCount is zero or negative this is the last message to retrieve (exclusive).
        /// If equal or less than zero, get messages from the end</param>
        /// <param name="maxCount">The maximum number of messages to retrieve, if negative retrieve message up until the pivot message id</param>
        /// <returns>An array of chat messages, can be empty or null if no new data exists</returns>
        Task<ChatMessage[]> GetMessages(String providerChatId, HttpServerRequest request, long pivotId, int maxCount);


        /// <summary>
        /// Post a user message to the given chat session. 
        /// </summary>
        /// <param name="providerChatId">The unique id of the chat session, as returned by the CreateNewChat method (or any static chat "channels" available)</param>
        /// <param name="request">The http request of the callee</param>
        /// <param name="message">The message to post</param>
        /// <returns>True if successful</returns>
        Task<bool> UserMessage(String providerChatId, HttpServerRequest request, ChatMessageBody message);

        /// <summary>
        /// Set a chat variable
        /// </summary>
        /// <param name="providerChatId">The unique id of the chat session, as returned by the CreateNewChat method (or any static chat "channels" available)</param>
        /// <param name="request">The http request of the callee</param>
        /// <param name="messageId">The ID of the message, 0 for none messge specific values</param>
        /// <param name="key">The key of the value</param>
        /// <param name="value"></param>
        /// <returns>True if successful</returns>
        Task<bool> SetValue(String providerChatId, HttpServerRequest request, long messageId, String key, String value);

        /// <summary>
        /// Called once when the chat service registers the instance.
        /// The supplied controller is used to push actions back to clients.
        /// </summary>
        /// <param name="controller">The controller to use for pushing actions to clients</param>
        void OnInit(IChatController controller);


        /// <summary>
        /// Get the chat message for the given message id, return null if it no longer exists
        /// </summary>
        /// <param name="providerChatId">The unique id of the chat session, as returned by the CreateNewChat method (or any static chat "channels" available)</param>
        /// <param name="messageId">The id of the message to retrieve</param>
        /// <param name="request">The http request of the callee</param>
        /// <returns>The requested message or null</returns>
        Task<ChatMessage> GetChatMessage(String providerChatId, long messageId, HttpServerRequest request);




    }

}
