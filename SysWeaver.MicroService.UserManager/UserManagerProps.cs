using System;

namespace SysWeaver.MicroService
{

    /// <summary>
    /// Misc properties about the user manager configuration
    /// </summary>
    public sealed class UserManagerProps
    {
        /// <summary>
        /// If login is available (login is still only available if the Login service is loaded).
        /// </summary>
        public bool CanLogin;
        /// <summary>
        /// If true, the SetNickName API is available
        /// </summary>
        public bool CanSetNickName;
        /// <summary>
        /// All communication methods supported by the user manager
        /// </summary>
        public String[] Coms;
        /// <summary>
        /// The sign up methods offered by the user manager: empty unless a <see cref="SignUpService"/> is loaded (it enables them, without it there is no sign up api)
        /// </summary>
        public String[] SignUpMethods;
        /// <summary>
        /// Number of digits to use in short codes
        /// </summary>
        public int ShortCodeDigits;
    }
}
