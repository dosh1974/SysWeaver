namespace SysWeaver.MicroService
{
    /// <summary>
    /// Errors returned when a user action fails
    /// </summary>
    public enum UserErrors
    {
        /// <summary>
        /// No error
        /// </summary>
        None = 0,
        /// <summary>
        /// The action token has expired or was already used
        /// </summary>
        TokenExpired,
        /// <summary>
        /// A user with that name or address already exist
        /// </summary>
        UserAlreadyExist,
        /// <summary>
        /// There is no session (for the request)
        /// </summary>
        NoSession,
        /// <summary>
        /// The user doesn't exist
        /// </summary>
        UserDoesntExist,
        /// <summary>
        /// An administrator required a new password (<see cref="UserManagerService.ForcePasswordReset"/>) and the chosen password is the
        /// current one (<see cref="UserManagerService.ResetPassword"/>); nothing was changed.
        /// </summary>
        SamePassword,
    };


}
