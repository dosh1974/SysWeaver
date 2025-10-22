using System;


namespace SysWeaver.MicroService
{

    public sealed class UserLoginResponse
    {
        public UserErrors Error { get; set; }
        public String Username { get; set; }

        public String[] Tokens { get; set; }
    }

}