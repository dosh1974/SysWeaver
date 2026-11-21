using System;

namespace SysWeaver.Chat
{


    /// <summary>
    /// A room of the <see cref="MySqlChat"/> provider: the permissions and options of a chat session plus optional rate limits.
    /// Defaults differ from <see cref="ChatSessionParams"/>: users can not remove their own messages, the speech name is "Chat" and uploads go to the "UserProtected" repository.
    /// </summary>
    public sealed class MySqlChatRoom : ChatSessionParams
    {

        /// <summary>
        /// Create a room with the defaults of this provider.
        /// </summary>
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
