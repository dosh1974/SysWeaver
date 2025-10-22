using System;
using System.Threading.Tasks;

namespace SysWeaver.Chat
{
    public interface IMySqlRoomProvider
    {
        /// <summary>
        /// Returns room parameters 
        /// </summary>
        /// <param name="id">The provided chat id</param>
        /// <returns>Room parameters or null if not available</returns>
        ValueTask<MySqlChatRoom> GetRoom(String id);
    }


}
