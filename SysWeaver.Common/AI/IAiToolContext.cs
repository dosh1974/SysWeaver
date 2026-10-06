using System;

namespace SysWeaver.AI
{
    /// <summary>
    /// The context of an AI tool invocation, lets a tool attach links and files to the result and keep state between calls.
    /// </summary>
    /// <remarks>
    /// Tool methods get the context from the request properties (key <c>OpenAiToolExt.RequestAiToolContext</c> in SysWeaver.HttpServer); it's null when the method isn't called as an AI tool.
    /// Implemented by the AI chat (backed by the chat session) and by the MCP service (backed by the HTTP session, or by the context itself when there is no session).
    /// </remarks>
    public interface IAiToolContext
    {
        /// <summary>
        /// Add a link to something (displayed in chat, or returned to an MCP client)
        /// </summary>
        /// <param name="url">The local or absolute url to the file</param>
        void AddLink(String url);

        /// <summary>
        /// Attach some data as a file to a message
        /// </summary>
        /// <param name="mime">The mimetype of the file</param>
        /// <param name="data">The data of the file, either a data url ("data:mime;base64,...") or text</param>
        /// <param name="filename">The name of the file, used when saving etc</param>
        /// <returns>The local url to the file</returns>
        String AddMessageFile(String mime, String data, String filename);

        /// <summary>
        /// Attach some data as a file to a message
        /// </summary>
        /// <param name="mime">The mimetype of the file</param>
        /// <param name="data">The binary data of the file</param>
        /// <param name="filename">The name of the file, used when saving etc</param>
        /// <returns>The local url to the file</returns>
        String AddMessageFile(String mime, ReadOnlyMemory<Byte> data, String filename);

        /// <summary>
        /// Assign a property for this chat session
        /// </summary>
        /// <typeparam name="T">The type of the value</typeparam>
        /// <param name="key">The property key</param>
        /// <param name="value">The value to store, replaces any existing value</param>
        void SetProperty<T>(String key, T value);

        /// <summary>
        /// Get a property previously assigned to the chat session
        /// </summary>
        /// <typeparam name="T">The expected type of the value</typeparam>
        /// <param name="key">The property key</param>
        /// <param name="value">The value if found, else default</param>
        /// <returns>True if the property was found</returns>
        bool TryGetProperty<T>(String key, out T value);


        /// <summary>
        /// Delete a property previously assigned to the chat session
        /// </summary>
        /// <typeparam name="T">The expected type of the value</typeparam>
        /// <param name="key">The property key</param>
        /// <param name="value">The removed value if found, else default</param>
        /// <returns>True if the property was found and removed</returns>
        bool TryRemoveProperty<T>(String key, out T value);

    }


}
