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
        /// Check if the supplied auth (of the user making the request) can join this session
        /// </summary>
        /// <param name="auth">The auth of the user making the request</param>
        /// <returns>True if the user should be able to join</returns>
        public bool CanJoin(Authorization auth) => auth.IsValid(Auth);

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
