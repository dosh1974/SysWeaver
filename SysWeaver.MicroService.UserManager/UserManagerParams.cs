using System;
using SysWeaver.Auth;
using SysWeaver.Db;


namespace SysWeaver.MicroService
{

    public class UserManagerParams : MySqlDbParams
    {

        public UserManagerParams()
        {
            Schema = "UserManager";
        }

        /// <summary>
        /// The requirements for passwords.
        /// </summary>
        public PasswordPolicy PasswordPolicy;

        /// <summary>
        /// The folder where localized messages resides.
        /// Can use a "*" to define a subfolder pattern, ex: "C:\locale\*\UserManager".
        /// </summary>
        public string LocalizedMessages;

        /// <summary>
        /// If true, user's can't login using this
        /// </summary>
        public bool DisableLogin;

        /// <summary>
        /// The system to use for text messages, if it's an existing filename, the system is read from the first line of text
        /// </summary>
        public String System;

        /// <summary>
        /// If true nick name may be changed
        /// </summary>
        public bool CanSetNickName = true;

        /// <summary>
        /// [3, 16] Number of digits to use in short codes
        /// </summary>
        public int ShortCodeDigits = 6;

        /// <summary>
        /// If true (and the server accepts basic auth, then users are allowed to login using basic auth)
        /// </summary>
        public bool AllowBasicAuth;


        /// <summary>
        /// The prefered order of communication with a user, comma separated.
        /// Corresponding coms service must be available and the user must have the corresponding info (email or phone number) for an communication attempt to be made.
        /// If null or empty, the messages are sent to all communication channels.
        /// Valid strings:
        /// - Email
        /// - WhatsApp
        /// - Sms
        /// </summary>
        public String ComsOrder = "Email, WhatsApp, Sms";

        /// <summary>
        /// Optionally specify a specific email coms instance (a coms instance with a matching name must exist).
        /// </summary>
        public String EmailInstance;

        /// <summary>
        /// Optionally specify a specific whats app coms instance (a coms instance with a matching name must exist).
        /// </summary>
        public String WhatsAppInstance;

        /// <summary>
        /// Optionally specify a specific text coms instance (a coms instance with a matching name must exist).
        /// </summary>
        public String SmsInstance;


    }


}
