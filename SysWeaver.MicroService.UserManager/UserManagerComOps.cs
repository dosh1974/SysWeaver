namespace SysWeaver.MicroService
{
    public enum UserManagerComOps
    {
        Invite,
        SignUp,
        DeleteUser,
        ResetPassword,
        AddPassword,
        DeletePassword,
        AddEmail,
        ChangeEmail,
        AddEmailNoCode,
        ChangeEmailNoCode,
        DeletedEmail,
        AddPhone,
        ChangePhone,
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
        Count,
    }

}
