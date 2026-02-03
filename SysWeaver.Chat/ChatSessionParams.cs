using System;
using SysWeaver.Auth;

namespace SysWeaver.Chat
{
    public class ChatSessionParams
    {
        /// <summary>
        /// Auth required to join, null to allow joining anonomously
        /// </summary>
        public String Auth = "";


        /// <summary>
        /// Auth required to post a message, null to use Auth
        /// </summary>
        public String PostAuth;


        /// <summary>
        /// Auth required to clear the chat room.
        /// Use '-' to disable everyone from clearing it.
        /// </summary>
        public String ClearAuth = "Admin";

        /// <summary>
        /// True to enable removal of messages sent by yourself
        /// </summary>
        public bool RemoveOwn = true;

        /// <summary>
        /// Auth required to remove any chat message.
        /// Use '-' to disable everyone from removing a chat message.
        /// </summary>
        public String RemoveAnyAuth = "Admin";


        /// <summary>
        /// Name of the room
        /// </summary>
        public String Name;

        /// <summary>
        /// If non-null, continous speech input will be enabled, listening to this keyword.
        /// </summary>
        public String SpeechName;

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
        /// If true, only enable the show porfile option if the user may post new messages
        /// </summary>
        public bool OnlyShowProfileIfPostIsAllowed = true;


        /// <summary>
        /// If non-empty and a IUserStorage is available, files can be uploaded
        /// </summary>
        public String UploadRepo;

        /// <summary>
        /// The maximum number of data items
        /// </summary>
        public int MaxDataCount = 10;





        /// <summary>
        /// Check if the supplied auth (of the user making the request) can join this session
        /// </summary>
        /// <param name="auth">The auth of the user making the request</param>
        /// <returns>True if the user should be able to join</returns>
        public bool CanJoin(Authorization auth) => auth.IsValid(Auth);

        /// <summary>
        /// Check if the supplied auth (of the user making the request) can join this session
        /// </summary>
        /// <param name="auth">The auth of the user making the request</param>
        /// <returns>True if the user should be able to join</returns>
        public bool CanPost(Authorization auth) => auth.IsValid(PostAuth ?? Auth);

        /// <summary>
        /// Check if the supplied auth (of the user making the request) can clear this session
        /// </summary>
        /// <param name="auth">The auth of the user making the request</param>
        /// <returns>True if the user should be able to clear the session</returns>
        public bool CanClear(Authorization auth)
        {
            var ca = ClearAuth;
            return ca == "-" ? false : auth.IsValid(ca);
        }

        /// <summary>
        /// Check what type of messages that the supplied auth (of the user making the request) can remove
        /// </summary>
        /// <param name="auth">The auth of the user making the request</param>
        /// <returns>What type of messages that the user should be able to remove from this session</returns>
        public ChatRemoveMessages CanRemove(Authorization auth)
        {
            var rv = RemoveOwn ? ChatRemoveMessages.Own : ChatRemoveMessages.None;
            var ra = RemoveAnyAuth;
            if (ra != "-")
                if (auth.IsValid(ra))
                    rv = ChatRemoveMessages.Any;
            return rv;
        }



    }

}
