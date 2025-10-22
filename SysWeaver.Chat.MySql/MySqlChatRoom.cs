using System;

namespace SysWeaver.Chat
{


    public sealed class MySqlChatRoom : ChatSessionParams
    {

        public MySqlChatRoom()
        {
            RemoveOwn = false;
        }

        /// <summary>
        /// Name of the room
        /// </summary>
        public String Name;

        /// <summary>
        /// If non-null, continous speech input will be enabled, listening to this keyword.
        /// </summary>
        public String SpeechName = "Chat";

        /// <summary>
        /// If true, enable speech by default
        /// </summary>
        public bool EnableSpeechByDefault;

        /// <summary>
        /// If true, the user may input markdown text (client is allowed to send the message with the MarkDown format).
        /// </summary>
        public bool AllowUserMarkDown = true;

        /// <summary>
        /// Allow storing files and links on the server (requires a UserStore).
        /// </summary>
        public bool AllowStore = true;

        /// <summary>
        /// If true, the server supports message translation (to the users language)
        /// </summary>
        public bool CanTranslate = true;

        /// <summary>
        /// If true, enable the menu option to show a user profile
        /// </summary>
        public bool CanShowProfile;

        /// <summary>
        /// If non-empty and a IUserStorage is available, files can be uploaded
        /// </summary>
        public String UploadRepo = "UserProtected";

        /// <summary>
        /// The maximum number of data items
        /// </summary>
        public int MaxDataCount = 10;

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
