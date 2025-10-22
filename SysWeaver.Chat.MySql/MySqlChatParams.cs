using SysWeaver.Db;

namespace SysWeaver.Chat
{
    public class MySqlChatParams : MySqlDbParams
    {
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
