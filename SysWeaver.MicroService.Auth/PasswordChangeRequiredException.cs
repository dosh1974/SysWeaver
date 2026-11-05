using System;
using System.Threading.Tasks;
using SysWeaver.Net;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Thrown by an authorizer (from <see cref="Auth.AuthorizerBase.SecureAuth"/>) when the user id and password are correct, but the password must be
    /// changed before the user may sign in (ex: an administrator forced a password reset, <c>UserManagerService.ForcePasswordReset</c>).
    /// No session is created: <see cref="LoginService.Login"/> answers with <see cref="AuthInfo.PasswordChangeRequired"/> set and calls
    /// <see cref="StartChange"/>, that sends the user the instructions (a link and a short code) to choose a new password.
    /// </summary>
    /// <remarks>Only thrown after the password was verified, so it never tells a caller without the password anything.</remarks>
    public sealed class PasswordChangeRequiredException : Exception
    {
        /// <summary>
        /// Create the exception.
        /// </summary>
        /// <param name="userName">The user name</param>
        /// <param name="startChange">Starts the password change for a request (sends the instructions), returns where they were sent (e-mail addresses / phone numbers), may be null</param>
        public PasswordChangeRequiredException(String userName, Func<HttpServerRequest, Task<String[]>> startChange)
            : base("The user " + userName.ToQuoted() + " must choose a new password before signing in!")
        {
            UserName = userName;
            StartChange = startChange;
        }

        /// <summary>
        /// The user that must change the password
        /// </summary>
        public readonly String UserName;

        /// <summary>
        /// Starts the password change for the request (ex: sends a reset link and a short code to the user), returns where the instructions were sent.
        /// May be null (then the user can't change it on their own).
        /// </summary>
        public readonly Func<HttpServerRequest, Task<String[]>> StartChange;
    }
}
