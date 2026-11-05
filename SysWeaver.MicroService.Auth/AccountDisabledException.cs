using System;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Thrown by an authorizer (from <see cref="Auth.AuthorizerBase.SecureAuth"/>) when the user id and password are correct, but an administrator
    /// disabled the account (ex: <c>UserManagerService.DisableUser</c>). No session is created: <see cref="LoginService.Login"/> answers with
    /// <see cref="AuthInfo.AccountDisabled"/> set, so the sign in page says "This account is disabled" instead of "Wrong user ID or password".
    /// </summary>
    /// <remarks>Only thrown after the password was verified, so it never tells a caller without the password anything.</remarks>
    public sealed class AccountDisabledException : Exception
    {
        /// <summary>
        /// Create the exception.
        /// </summary>
        /// <param name="userName">The user name</param>
        public AccountDisabledException(String userName)
            : base("The account " + userName.ToQuoted() + " is disabled!")
        {
            UserName = userName;
        }

        /// <summary>
        /// The user of the disabled account
        /// </summary>
        public readonly String UserName;
    }
}
