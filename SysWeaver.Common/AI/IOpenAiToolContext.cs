using System;

namespace SysWeaver.AI
{
    public interface IOpenAiToolContext
    {
        /// <summary>
        /// Add a link to something (displayed in chat)
        /// </summary>
        /// <param name="url">The local or absolute url to the file</param>
        void AddLink(String url);

        /// <summary>
        /// Attach some data as a file to a message
        /// </summary>
        /// <param name="mime">The mimetype of the file</param>
        /// <param name="data">The data of the file</param>
        /// <param name="filename">The name of the file, used when saving etc</param>
        /// <returns>The local url to the file</returns>
        String AddMessageFile(String mime, String data, String filename);

        /// <summary>
        /// Attach some data as a file to a message
        /// </summary>
        /// <param name="mime">The mimetype of the file</param>
        /// <param name="data">The data of the file</param>
        /// <param name="filename">The name of the file, used when saving etc</param>
        /// <returns>The local url to the file</returns>
        String AddMessageFile(String mime, ReadOnlyMemory<Byte> data, String filename);

        /// <summary>
        /// Assign a property for this chat session
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="value"></param>
        void SetProperty<T>(String key, T value);

        /// <summary>
        /// Get a property previously assigned to the chat session
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="value"></param>
        bool TryGetProperty<T>(String key, out T value);


        /// <summary>
        /// Delete a property previously assigned to the chat session
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="value"></param>
        bool TryRemoveProperty<T>(String key, out T value);

    }


}
