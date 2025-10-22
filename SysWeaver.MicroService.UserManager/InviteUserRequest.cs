using System;


namespace SysWeaver.MicroService
{
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
        /// Optional security tokens to give the user (the inviter must have them)
        /// </summary>
        public String[] Tokens { get; set; }

        /// <summary>
        /// Application specific domain (the inviter must be part of the same domain)
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
    }


}
