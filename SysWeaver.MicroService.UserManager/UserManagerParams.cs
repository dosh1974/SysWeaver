using System;
using SysWeaver.Auth;
using SysWeaver.Db;


namespace SysWeaver.MicroService
{

    /// <summary>
    /// Parameters for the <see cref="UserManagerService"/>
    /// </summary>
    public class UserManagerParams : MySqlDbParams
    {

        /// <summary>
        /// Create the parameters, the database schema defaults to "UserManager"
        /// </summary>
        public UserManagerParams()
        {
            Schema = "UserManager";
        }

        /// <summary>
        /// The requirements for passwords.
        /// If null, the default <see cref="Auth.PasswordPolicy"/> is used.
        /// </summary>
        public PasswordPolicy PasswordPolicy;

        /// <summary>
        /// The folder where localized messages resides.
        /// Can use a "*" to define a subfolder pattern, ex: "C:\locale\*\UserManager".
        /// </summary>
        public string LocalizedMessages;

        /// <summary>
        /// If true, users can't sign in using this user manager.
        /// Defaults to false.
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
        /// [3, 16] Number of digits to use in short codes (values outside the range are clamped).
        /// Defaults to 6.
        /// </summary>
        public int ShortCodeDigits = 6;

        /// <summary>
        /// The wrong short code tries allowed per account, from all browsers together (every limit must allow a try; a right code is never counted).
        /// Kept in memory only (a restart forgets them). Null or empty for no limit per account (each code still allows 10 tries).
        /// Default: 3 per 5 minutes and 50 per day
        /// </summary>
        public RateLimiterParams[] CodeTriesPerUser =
        [
            new RateLimiterParams { Count = 3, Duration = 5 * 60 },
            new RateLimiterParams { Count = 50, Duration = 24 * 60 * 60 },
        ];

        /// <summary>
        /// The string that binds the stored password verifiers to this application (part of the PBKDF2 input, see <see cref="SysWeaver.Auth.ScramInfo"/>).
        /// null (the default) uses the entry assembly name. Applications that share a user database must use the same value.
        /// Changing it makes all stored passwords invalid (users must reset their passwords).
        /// </summary>
        public String PasswordHashString;

        /// <summary>
        /// The PBKDF2 iteration count of new password verifiers, 0 or less uses <see cref="SysWeaver.Auth.ScramTools.DefaultIterations"/>.
        /// Existing verifiers keep their iteration count (until the password is changed).
        /// </summary>
        public int ScramIterations;

        /// <summary>
        /// The secret (base64, at least 16 bytes) that protects the user names, nick names, e-mail addresses and phone numbers in the database, see <see cref="SysWeaver.Auth.DataProtection"/>.
        /// null (the default) uses <see cref="SysWeaver.Auth.DataProtection.Default"/>: a random secret stored in the key value store of the application on this computer.
        /// Set it when several applications or computers share the database (all must use the same secret).
        /// IMPORTANT: without the secret the protected values can't be read or found (keep a backup); a database protected with another secret is refused (nothing is read or written).
        /// </summary>
        public String DataProtectionSecret;

        /// <summary>
        /// Optional: a file with the data protection secret (base64), ex: "$(KeyFolder)\HypnosUsers_DataProtection.txt" (used when <see cref="DataProtectionSecret"/> is null).
        /// The file is created with a new random secret if it doesn't exist. Every service that shares the user database must use the same secret (the same file, or a copy of it).
        /// Back it up: without it the user names, e-mail addresses and phone numbers can't be read.
        /// </summary>
        public String DataProtectionSecretFile;


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
