using System;

namespace SysWeaver.MicroService
{

    public interface IUserCoreData
    {
        String UserName { get; }
        String Language { get; }

        String NickName { get; }

    }

    public class UserData : IUserCoreData
    {
        public String UserName { get; set; }
        public String Email { get; set; }

        public String Phone { get; set; }

        public String[] Tokens { get; set; }

        public String Domain { get; set; }
        public String Language { get; set; }

        public String NickName { get; set; }
    }

    public sealed class NewUserData : UserData
    {
        public String Salt { get; set; }

        /// <summary>
        /// The user name of the inviter, null for a sign up (or an invitation made before this was recorded)
        /// </summary>
        public String InvitedBy { get; set; }

    }



    public class NewPasswordData
    {
        public String UserName { get; set; }
        public String NickName { get; set; }
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



    public sealed class InternalNewPasswordData : NewPasswordData
    {
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
