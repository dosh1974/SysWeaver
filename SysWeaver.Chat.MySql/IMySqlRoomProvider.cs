using System;
using System.Threading.Tasks;

namespace SysWeaver.Chat
{
    /// <summary>
    /// Provides rooms for <see cref="MySqlChat"/> that aren't defined in its parameters (looked up by chat id).
    /// </summary>
    public interface IMySqlRoomProvider
    {
        /// <summary>
        /// Returns room parameters 
        /// </summary>
        /// <param name="id">The provided chat id</param>
        /// <returns>Room parameters or null if not available</returns>
        Task<MySqlChatRoom> GetRoom(String id);
    }


}
