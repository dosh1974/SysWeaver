using System;
using System.Linq;

namespace SysWeaver.Chat
{
    public class ChatMenuItem
    {
        /// <summary>
        /// The id used when setting a chat value.
        /// </summary>
        public String Id;

        /// <summary>
        /// The display name (shown in menu)
        /// </summary>
        public String Name;

        /// <summary>
        /// A title (description) shown in menu when hovering.
        /// </summary>
        public String Desc;

        /// <summary>
        /// Icon class to use
        /// </summary>
        public String Icon;
        
        /// <summary>
        /// The value to set when this is clicked (only valid if there are no children)
        /// </summary>
        public String Value;

        /// <summary>
        /// The auth required for this
        /// </summary>
        public String Auth;

        /// <summary>
        /// Any child items
        /// </summary>
        public ChatMenuItem[] Children;


        /// <summary>
        /// Copy everything but the children
        /// </summary>
        /// <param name="dest"></param>
        public void CopyTo(ChatMenuItem dest)
        {
            dest.Id = Id;
            dest.Name = Name;
            dest.Desc = Desc;
            dest.Icon = Icon;
            dest.Value = Value;
            dest.Auth = Auth;
        }

        /// <summary>
        /// Make a clone
        /// </summary>
        /// <returns></returns>
        public ChatMenuItem Clone(bool children = true)
        {
            var t = new ChatMenuItem();
            CopyTo(t);
            if (children)
                t.Children = Children?.Select(x => x.Clone())?.ToArray();
            return t;
        }

    }

}
