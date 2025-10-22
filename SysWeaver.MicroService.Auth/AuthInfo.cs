using System;
using System.Threading.Tasks;

namespace SysWeaver.MicroService
{
    public sealed class AuthInfo
    {
        /// <summary>
        /// True if a user is logged in to the session
        /// </summary>
        public bool Succeeded { get; set; }

        /// <summary>
        /// The language code of the user / session
        /// </summary>
        public String Language { get; set; }

        /// <summary>
        /// Guid (can be used for user image etc)
        /// </summary>
        public String Guid { get; set; }

        /// <summary>
        /// The unique account name of the user
        /// </summary>
        public String Username { get; set; }


        /// <summary>
        /// The non-unique customizable nick name of the user (defaults to a random name)
        /// </summary>
        public String NickName { get; set; }

        /// <summary>
        /// If true, the nick name was auto selected
        /// </summary>
        public bool AutoNickName { get; set; }


        /// <summary>
        /// The domain of the user (the domain meaning is application specific)
        /// </summary>
        public String Domain { get; set; }

        /// <summary>
        /// The security tokens that this user have
        /// </summary>
        public String[] Tokens { get; set; }


        public static readonly AuthInfo Failed = new AuthInfo();

        public static readonly Task<AuthInfo> TaskFailed = Task.FromResult(Failed);

    }







}
