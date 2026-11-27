using System;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Information about a user
    /// </summary>
    public sealed class UserInfo : IUserCoreData
    {
        /// <summary>
        /// The id of the user
        /// </summary>
        public long Id;

        /// <summary>
        /// The globally unique id of the user (as used for the authentication of sessions)
        /// </summary>
        public String Guid;

        /// <summary>
        /// The e-mail address of the user, null if none
        /// </summary>
        public String Email;

        /// <summary>
        /// The phone number of the user, null if none
        /// </summary>
        [SysWeaver.Data.TableDataPhone]
        public String Phone;


        /// <summary>
        /// When the user was created
        /// </summary>
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
