namespace SysWeaver.MicroService
{
    /// <summary>
    /// The messages that the user manager sends to users
    /// </summary>
    public enum UserManagerComOps
    {
        /// <summary>
        /// An invitation to create an account
        /// </summary>
        Invite,
        /// <summary>
        /// The message that confirms a sign up
        /// </summary>
        SignUp,
        /// <summary>
        /// The message that confirms that an account is to be deleted
        /// </summary>
        DeleteUser,
        /// <summary>
        /// Instructions on how to reset the password (when the user asked for it)
        /// </summary>
        ResetPassword,
        /// <summary>
        /// The message that confirms adding a password
        /// </summary>
        AddPassword,
        /// <summary>
        /// The message that confirms removing the password
        /// </summary>
        DeletePassword,
        /// <summary>
        /// The message that confirms adding an e-mail address (with a code)
        /// </summary>
        AddEmail,
        /// <summary>
        /// The message that confirms changing the e-mail address (with a code)
        /// </summary>
        ChangeEmail,
        /// <summary>
        /// The message that confirms adding an e-mail address (without a code)
        /// </summary>
        AddEmailNoCode,
        /// <summary>
        /// The message that confirms changing the e-mail address (without a code)
        /// </summary>
        ChangeEmailNoCode,
        /// <summary>
        /// The notice that an e-mail address was removed from the account
        /// </summary>
        DeletedEmail,
        /// <summary>
        /// The message that confirms adding a phone number
        /// </summary>
        AddPhone,
        /// <summary>
        /// The message that confirms changing the phone number
        /// </summary>
        ChangePhone,
        /// <summary>
        /// The notice that a phone number was removed from the account
        /// </summary>
        DeletedPhone,
        /// <summary>
        /// The reset instructions when an administrator required a new password (instead of <see cref="ResetPassword"/>, which says that the user asked for it).
        /// Localized message folders (<see cref="UserManagerParams.LocalizedMessages"/>) that have no such template use the <see cref="ResetPassword"/> template.
        /// </summary>
        ForcedResetPassword,
        /// <summary>
        /// The notice sent to the old e-mail address after the e-mail address of an account was changed (e-mail only, no link to undo it:
        /// the account has a newer address).
        /// Localized message folders (<see cref="UserManagerParams.LocalizedMessages"/>) that have no such template use the built in (English) template.
        /// </summary>
        ChangedEmail,
        // ---- MUST BE LAST --------------------
        /// <summary>
        /// The number of messages (not a message)
        /// </summary>
        Count,
    }

}
