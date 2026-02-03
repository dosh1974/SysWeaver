using System;

namespace SysWeaver.Chat
{


    public sealed class MySqlChatRoom : ChatSessionParams
    {

        public MySqlChatRoom()
        {
            RemoveOwn = false;
            SpeechName = "Chat";
            UploadRepo = "UserProtected";
        }

        /// <summary>
        /// (Optional) rate limiter parameters for this channel
        /// </summary>
        public HttpRateLimiterParams ServiceLimiter;

        /// <summary>
        /// (Optional) session limiter parameters for this channel
        /// </summary>
        public HttpRateLimiterParams SessionLimiter;


    }


}
