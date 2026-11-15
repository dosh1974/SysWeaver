using System;
using System.Threading.Tasks;
using SysWeaver.Net;

namespace SysWeaver.AI
{

    /// <summary>
    /// A per user memory that the AI can use to store and retrieve information between conversations.
    /// See <see cref="AiMemoryLimits"/> for the size limits.
    /// </summary>
    public interface IAiMemory
    {
        /// <summary>
        /// Get the headers (key, last access time and description) of all memories of the user
        /// </summary>
        /// <param name="session">The session of the user</param>
        /// <returns>The memory headers</returns>
        Task<AiMemoryEntry[]> GetMemoryEntries(HttpSession session);

        /// <summary>
        /// Add a memory (or replace an existing memory with the same key)
        /// </summary>
        /// <param name="session">The session of the user</param>
        /// <param name="key">The unique key of the memory</param>
        /// <param name="desc">A short description of the memory</param>
        /// <param name="value">The value of the memory</param>
        /// <returns>True on success</returns>
        /// <exception cref="Exception">If an argument is invalid (empty or too long) or there are too many memories</exception>
        Task<bool> AddMemory(HttpSession session, String key, String desc, String value);

        /// <summary>
        /// Get the value of a memory
        /// </summary>
        /// <param name="session">The session of the user</param>
        /// <param name="key">The key of the memory</param>
        /// <returns>The value, or null if there is no memory with the key</returns>
        Task<String> GetMemory(HttpSession session, String key);

        /// <summary>
        /// Change the value of an existing memory
        /// </summary>
        /// <param name="session">The session of the user</param>
        /// <param name="key">The key of the memory</param>
        /// <param name="value">The new value</param>
        /// <returns>True on success</returns>
        /// <exception cref="Exception">If the value is invalid or the memory doesn't exist</exception>
        Task<bool> SetMemory(HttpSession session, String key, String value);

        /// <summary>
        /// Remove a memory
        /// </summary>
        /// <param name="session">The session of the user</param>
        /// <param name="key">The key of the memory</param>
        /// <returns>True on success</returns>
        /// <exception cref="Exception">If the memory doesn't exist</exception>
        Task<bool> RemoveMemory(HttpSession session, String key);
    }

}
