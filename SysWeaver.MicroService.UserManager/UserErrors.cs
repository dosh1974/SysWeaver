namespace SysWeaver.MicroService
{
    public enum UserErrors
    {
        None = 0,
        TokenExpired,
        UserAlreadyExist,
        NoSession,
        UserDoesntExist,
        /// <summary>
        /// An administrator required a new password (<see cref="UserManagerService.ForcePasswordReset"/>) and the chosen password is the
        /// current one (<see cref="UserManagerService.ResetPassword"/>); nothing was changed.
        /// </summary>
        SamePassword,
    };


}
