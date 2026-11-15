using System;

namespace SysWeaver.MicroService
{

    /// <summary>
    /// The core data of a user
    /// </summary>
    public interface IUserCoreData
    {
        /// <summary>
        /// The name of the user
        /// </summary>
        String UserName { get; }

        /// <summary>
        /// Preferred language of the user
        /// </summary>
        String Language { get; }

        /// <summary>
        /// Optional information for the user
        /// </summary>
        String NickName { get; }

    }

    /// <summary>
    /// The data of a user (to be created)
    /// </summary>
    public class UserData : IUserCoreData
    {
        /// <inheritdoc />
        public String UserName { get; set; }

        /// <summary>
        /// The e-mail address of the user, can be null
        /// </summary>
        public String Email { get; set; }

        /// <summary>
        /// The phone number of the user, can be null
        /// </summary>
        public String Phone { get; set; }

        /// <summary>
        /// The security tokens of the user
        /// </summary>
        public String[] Tokens { get; set; }

        /// <summary>
        /// The application specific domain of the user, null for the default
        /// </summary>
        public String Domain { get; set; }

        /// <inheritdoc />
        public String Language { get; set; }

        /// <inheritdoc />
        public String NickName { get; set; }
    }

    /// <summary>
    /// The data of a user that is about to be created (stored with the action token)
    /// </summary>
    public sealed class NewUserData : UserData
    {
        /// <summary>
        /// The salt that the client must use when hashing the password of the new user
        /// </summary>
        public String Salt { get; set; }

        /// <summary>
        /// The user name of the inviter, null for a sign up (or an invitation made before this was recorded)
        /// </summary>
        public String InvitedBy { get; set; }

    }



    /// <summary>
    /// The information needed by the client to choose a new password (from an action token)
    /// </summary>
    public class NewPasswordData
    {
        /// <summary>
        /// The name of the user
        /// </summary>
        public String UserName { get; set; }

        /// <summary>
        /// Optional information for the user
        /// </summary>
        public String NickName { get; set; }

        /// <summary>
        /// The salt that the client must use when hashing the new password
        /// </summary>
        public String Salt { get; set; }

        /// <summary>
        /// True if an administrator required the new password (<see cref="UserManagerService.ForcePasswordReset"/>): the user can't sign in with
        /// the current password until a new one is chosen, and the new password must differ from it.
        /// </summary>
        public bool Forced { get; set; }

        /// <summary>
        /// When <see cref="Forced"/>: the salt of the current password, so the client can also send the hash of the new password with it
        /// (<see cref="AddUserRequest.CurrentHash"/>) and the server can refuse the same password. Null otherwise.
        /// </summary>
        public String CurrentSalt { get; set; }

    }



    /// <summary>
    /// <see cref="NewPasswordData"/> as stored with the action token (includes the id of the user)
    /// </summary>
    public sealed class InternalNewPasswordData : NewPasswordData
    {
        /// <summary>
        /// The id of the user
        /// </summary>
        public long UserId { get; set; }

    }

    sealed class InternalNewEmailData
    {
        public long UserId { get; set; }
        public String Email { get; set; }

    }

    sealed class InternalNewPhoneData
    {
        public long UserId { get; set; }
        public String Phone { get; set; }

    }


}
