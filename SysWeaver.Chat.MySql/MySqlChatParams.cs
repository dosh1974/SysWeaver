using SysWeaver.Db;

namespace SysWeaver.Chat
{
    /// <summary>
    /// Parameters of the <see cref="MySqlChat"/> provider: the MySQL connection (schema "Chat" by default), the provider id and the rooms.
    /// </summary>
    public class MySqlChatParams : MySqlDbParams
    {
        /// <summary>
        /// Create the parameters with the "Chat" schema.
        /// </summary>
        public MySqlChatParams()
        {
            Schema = "Chat";
        }

        /// <summary>
        /// Chat provider id 
        /// </summary>
        public string ProviderId = "MySql";

        /// <summary>
        /// Default rooms
        /// </summary>
        public MySqlChatRoom[] Rooms;

    }


}
