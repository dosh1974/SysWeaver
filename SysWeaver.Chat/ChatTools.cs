using System;
using SysWeaver.Net;

namespace SysWeaver.Chat
{
    public static class ChatTools
    {
        /// <summary>
        /// Get an anonymous user name for the given session
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        public static String GetAnonymousUsername(HttpSession session)
        {
            if (session == null)
                return "Anonymous";
            var t = String.GetHashCode(session.DeviceId);
            if (t < 0)
                t = -t;
            return "Anonymous" + t;
        }

        /// <summary>
        /// Get the user name from a session
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        public static String GetUsername(HttpSession session)
        {
            var nick = session?.Auth?.NickName;
            if (!String.IsNullOrEmpty(nick))
                return nick; 
            return GetAnonymousUsername(session);
        }

    }

}
