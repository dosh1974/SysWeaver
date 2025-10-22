using System;


namespace SysWeaver.MicroService
{
    public sealed class PhoneComsParams : ComsParams
    {

        /// <summary>
        /// An optional instance name of the ITextMessageSender and ITextMessageReceiverService to use.
        /// If null, the first instance will be used.
        /// Default: "useradmin".
        /// </summary>
        public String Instance = "useradmin";

        /// <summary>
        /// An optional template to use for invite texts (if this is an existing file, content is read from that).
        /// </summary>
        public String Invite;

        /// <summary>
        /// An optional template to use for sign up texts (if this is an existing file, content is read from that).
        /// </summary>
        public String SignUp;

        /// <summary>
        /// An optional template to use for delete user texts (if this is an existing file, content is read from that).
        /// </summary>
        public String DeleteUser;

        /// <summary>
        /// An optional template to use for reset password texts (if this is an existing file, content is read from that).
        /// </summary>
        public String ResetPassword;

        /// <summary>
        /// An optional template to use for add password texts (if this is an existing file, content is read from that).
        /// </summary>
        public String AddPassword;

        /// <summary>
        /// An optional template to use for delete password texts (if this is an existing file, content is read from that).
        /// </summary>
        public String DeletePassword;

        /// <summary>
        /// An optional template to use for add email address texts (if this is an existing file, content is read from that).
        /// </summary>
        public String AddEmail;

        /// <summary>
        /// An optional template to use for changing email address texts (if this is an existing file, content is read from that).
        /// </summary>
        public String ChangeEmail;

        /// <summary>
        /// An optional template to use for add email address texts without a code (if this is an existing file, content is read from that).
        /// </summary>
        public String AddEmailNoCode;

        /// <summary>
        /// An optional template to use for changing email address texts without a code (if this is an existing file, content is read from that).
        /// </summary>
        public String ChangeEmailNoCode;


        /// <summary>
        /// An optional template to use for deleted email address texts (if this is an existing file, content is read from that).
        /// </summary>
        public String DeletedEmail;


        /// <summary>
        /// An optional template to use for add phone number texts (if this is an existing file, content is read from that).
        /// </summary>
        public String AddPhone;

        /// <summary>
        /// An optional template to use for changing phone number texts (if this is an existing file, content is read from that).
        /// </summary>
        public String ChangePhone;

        /// <summary>
        /// An optional template to use for deleted phone number texts (if this is an existing file, content is read from that).
        /// </summary>
        public String DeletedPhone;


        /// <summary>
        /// An optional template to include as header for texts (if this is an existing file, content is read from that).
        /// </summary>
        public String Header;

        /// <summary>
        /// An optional template to include as footer for texts (if this is an existing file, content is read from that).
        /// </summary>
        public String Footer;


    }

}
