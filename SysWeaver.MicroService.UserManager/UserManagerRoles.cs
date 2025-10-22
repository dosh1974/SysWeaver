using System;

namespace SysWeaver.MicroService
{
    public static class UserManagerRoles
    {
        public const String InviteUser = "UserManagement";
        public const String ViewUser = Roles.Ops + "UserManagement,Debug";
        public const String PasswordReset = "UserManagement";

    }


    public static class UserManagerTools
    {
        public const int MaxDataStringLength = 256;

        public const int MaxDataKeyLength = 32;


    }

}
