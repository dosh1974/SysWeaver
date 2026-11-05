using System;


namespace SysWeaver.MicroService
{
    /// <summary>
    /// The input of <see cref="UserManagerService.InviteUser"/>: who to invite and what the new account gets.
    /// </summary>
    public sealed class InviteUserRequest
    {
        /// <summary>
        /// Optional name of the user
        /// </summary>
        [EditAllowNull]
        public String Name { get; set; }

        /// <summary>
        /// Email address, phone number or other supported communication method of the user to invite
        /// </summary>
        [EditMin(3)]
        public String Email { get; set; }

        /// <summary>
        /// Optional security tokens to give the user: tokens the inviter has; an Admin may also give any token except Debug, a Debug user any token; other tokens are left out
        /// </summary>
        public String[] Tokens { get; set; }

        /// <summary>
        /// Application specific domain of the new account: empty (null) for the inviter's own domain; another domain only when the inviter is Debug or Admin (else 403)
        /// </summary>
        public String Domain { get; set; }

        /// <summary>
        /// The language to use for messages
        /// </summary>
        public String Language { get; set; }
    }                                                                                                                                                                                                                                                                                                                                           


    public sealed class AddUserRequest
    {
        public String Token { get; set; }
        
        public String NewHash { get; set; }

        /// <summary>
        /// Optional (<see cref="UserManagerService.ResetPassword"/> only): the hash of the new password made with the salt of the current password
        /// (<see cref="NewPasswordData.CurrentSalt"/>). When an administrator required the new password (<see cref="NewPasswordData.Forced"/>) a hash
        /// that equals the current password is refused (the result has the error <see cref="UserErrors.SamePassword"/>). Null (older clients): not checked.
        /// </summary>
        public String CurrentHash { get; set; }
    }


}
