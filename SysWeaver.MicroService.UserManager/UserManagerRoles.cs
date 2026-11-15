using System;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// The security tokens of the user management APIs and pages (the original roles: only UserManagement may invite and change other users;
    /// Ops, Debug and UserManagement may see them).
    /// </summary>
    public static class UserManagerRoles
    {
        /// <summary>
        /// Invite a user (<see cref="UserManagerService.InviteUser"/>, the pending invitations): UserManagement
        /// </summary>
        public const String InviteUser = "UserManagement";
        /// <summary>
        /// See the user accounts (<see cref="UserManagerService.UserDataTable"/>, the Users page): Ops, Debug or UserManagement.
        /// </summary>
        /// <remarks>The original value was <c>Roles.Ops + "UserManagement,Debug"</c>, a missing comma that made it "Ops,DebugUserManagement,Debug":
        /// a UserManagement user could invite but not see the users. The comma is added (UserManagement can see the users it manages).</remarks>
        public const String ViewUser = Roles.Ops + ",UserManagement";
        /// <summary>
        /// Force a password reset (<see cref="UserManagerService.ForcePasswordReset"/>): UserManagement
        /// </summary>
        public const String PasswordReset = "UserManagement";
        /// <summary>
        /// Unlock (undo a forced password reset), disable / enable and delete user accounts (<see cref="UserManagerService.UnlockUser"/>,
        /// <see cref="UserManagerService.DisableUser"/>, <see cref="UserManagerService.EnableUser"/>, <see cref="UserManagerService.AdminDeleteUser"/>):
        /// UserManagement, the same as <see cref="PasswordReset"/> (the original role that may change other users)
        /// </summary>
        public const String ManageUser = PasswordReset;

    }


    /// <summary>
    /// Limits for the user data of the user manager
    /// </summary>
    public static class UserManagerTools
    {
        /// <summary>
        /// The maximum length of a user data string value
        /// </summary>
        public const int MaxDataStringLength = 256;

        /// <summary>
        /// The maximum length of a user data key
        /// </summary>
        public const int MaxDataKeyLength = 32;


    }

}
