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
        /// All sign up methods supported by the user manager (sign up is still only available if the SignUp service is loaded)
        /// </summary>
        public String[] SignUpMethods;
        /// <summary>
        /// Number of digits to use in short codes
        /// </summary>
        public int ShortCodeDigits;
    }
}
