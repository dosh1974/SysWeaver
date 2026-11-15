using System;


namespace SysWeaver.MicroService
{

    /// <summary>
    /// The response when a user was signed in by an action (<see cref="UserManagerService.AddUser"/> or <see cref="UserManagerService.ResetPassword"/>)
    /// </summary>
    public sealed class UserLoginResponse
    {
        /// <summary>
        /// The error, <see cref="UserErrors.None"/> on success
        /// </summary>
        public UserErrors Error { get; set; }

        /// <summary>
        /// The name of the signed in user (not set if there was an error)
        /// </summary>
        public String Username { get; set; }

        /// <summary>
        /// The security tokens of the signed in user (not set if there was an error)
        /// </summary>
        public String[] Tokens { get; set; }
    }

}