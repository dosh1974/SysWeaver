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

        /// <summary>
        /// Only set by a failed <see cref="LoginService.Login"/>: the user id and password were correct, but the user must choose a new password
        /// first (ex: an administrator forced a password reset). No user is signed in; the instructions were sent to <see cref="SentTo"/>
        /// (the short code works in this session, see auth/ResetPassword.html).
        /// </summary>
        public bool PasswordChangeRequired { get; set; }

        /// <summary>
        /// With <see cref="PasswordChangeRequired"/>: where the instructions to choose a new password were sent (e-mail addresses and / or phone numbers),
        /// null or empty if they could not be sent (the user must ask an administrator).
        /// </summary>
        public String[] SentTo { get; set; }

        /// <summary>
        /// Only set by a failed <see cref="LoginService.Login"/>: the user id and password were correct, but an administrator disabled the account
        /// (<see cref="AccountDisabledException"/>). No user is signed in. Only told to a caller that knows the password.
        /// </summary>
        public bool AccountDisabled { get; set; }


        public static readonly AuthInfo Failed = new AuthInfo();

        public static readonly Task<AuthInfo> TaskFailed = Task.FromResult(Failed);

    }







}
