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

    }



    public class NewPasswordData
    {
        public String UserName { get; set; }
        public String NickName { get; set; }
        public String Salt { get; set; }

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
