using System;

namespace SysWeaver.MicroService
{
    public sealed class UserInfo : IUserCoreData
    {
        public long Id;
        
        public String Guid;

        public String Email;
        
        public String Phone;
        
        
        public DateTime Created;

        /// <summary>
        /// Preferred language of the user
        /// </summary>
        public String Language { get; set; }

        /// <summary>
        /// The name of the user
        /// </summary>
        public String UserName { get; set; }

        /// <summary>
        /// Optional information for the user
        /// </summary>
        public String NickName { get; set; }

        /// <summary>
        /// If true, the nick name was auto selected
        /// </summary>
        public bool AutoNickName;

    }
}
