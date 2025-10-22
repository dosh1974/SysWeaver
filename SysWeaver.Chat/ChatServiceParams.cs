using System;

namespace SysWeaver.Chat
{
    public sealed class ChatServiceParams
    {
        /// <summary>
        /// If true, the user can store files in chat in a way that anyone can look at them
        /// </summary>
        public bool AllowPublicStore = true;

        /// <summary>
        /// Array of input languages that are allowed (can be selected).
        /// If null, the setup in the http server is used.
        /// </summary>
        public String[] InputLanguages;
    }

}
